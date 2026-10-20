using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class StokHesaplamaService : IStokHesaplamaService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StokHesaplamaService> _log;

        public StokHesaplamaService(ApplicationDbContext db, ILogger<StokHesaplamaService> log)
        {
            _db = db;
            _log = log;
        }

        // ═══════════════════════════════════════════════════════════
        //  HASTANE BAZLI HESAPLAMA
        // ═══════════════════════════════════════════════════════════
        public async Task<HastaneStokPaneliVm?> HesaplaHastaneAsync(
            int hospitalId,
            int hedefRunwayAy = 3,
            CancellationToken ct = default)
        {
            // 1) Hastane
            var hospital = await _db.Hospitals
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.HospitalID == hospitalId, ct);
            if (hospital == null) return null;

            var panel = new HastaneStokPaneliVm
            {
                HospitalID = hospital.HospitalID,
                HospitalName = hospital.HospitalName,
                HedefRunwayAy = hedefRunwayAy
            };

            // 2) Reaktif snapshot'lar (MappedTestID dolu)
            var snapshots = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => s.HospitalID == hospitalId
                         && s.SayimAktifMi
                         && s.UrunTipi == UrunTipi.Reaktif
                         && s.MappedTestID.HasValue)
                .ToListAsync(ct);

            if (snapshots.Count == 0)
            {
                _log.LogWarning("Hastane {HospitalID} için reaktif snapshot bulunamadı.", hospitalId);
                return panel;
            }

            panel.SonSayimNo = snapshots.First().SayimNo;
            panel.SonSayimTarihi = snapshots.Max(s => s.SayimTarihi);

            // 3) TestID + Tests tablosundan birim bilgisi
            var testIdler = snapshots.Select(s => s.MappedTestID!.Value).Distinct().ToList();

            //  ★ CHANGED: GirisTipi + KutuBasinaAdet alanlarını da çek
            var testler = await _db.Tests
                .AsNoTracking()
                .Where(t => testIdler.Contains(t.TestID))
                .Select(t => new
                {
                    t.TestID,
                    t.TestName,
                    t.SutCode,
                    t.GirisTipi,
                    t.KutuBasinaAdet
                })
                .ToListAsync(ct);
            var testById = testler.ToDictionary(t => t.TestID);

            // 4) Gönderim & kullanım dataları
            var tarihSinir = DateTime.Now.AddMonths(-6);
            var tarihSinirMonth = new DateTime(tarihSinir.Year, tarihSinir.Month, 1);

            var gonderimler = await _db.MonthlyShipments
                .AsNoTracking()
                .Where(m => m.HospitalID == hospitalId
                         && testIdler.Contains(m.TestID)
                         && m.Month >= tarihSinirMonth)
                .Select(m => new { m.TestID, m.Month, m.SentCount })
                .ToListAsync(ct);

            var kullanimlar = await _db.MonthlyStatistics
                .AsNoTracking()
                .Where(s => s.HospitalID == hospitalId
                         && testIdler.Contains(s.TestID)
                         && s.Month >= tarihSinirMonth)
                .Select(s => new { s.TestID, s.Month, s.TestCount })
                .ToListAsync(ct);

            var gonderimByTest = gonderimler
                .GroupBy(x => x.TestID)
                .ToDictionary(g => g.Key, g => g.ToList());
            var kullanimByTest = kullanimlar
                .GroupBy(x => x.TestID)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 5) ★ Snapshot'ları MappedTestID'ye göre GRUPLA — aynı ürünün farklı lot'ları
            //    tek satırda birleşsin. TestCount lot'ların toplamı olur.
            var urunGruplari = snapshots
                .GroupBy(s => s.MappedTestID!.Value)
                .ToList();

            foreach (var grp in urunGruplari.OrderBy(g => g.First().ProductName))
            {
                var testId = grp.Key;
                var testInfo = testById.TryGetValue(testId, out var t) ? t : null;
                var snap = grp.First();  // meta bilgi (ReferenceNumber, Platform vb.) için

                // ★ Lot'lar arası TOPLA
                int toplamTestCount = grp.Sum(s => s.TestCount);
                int toplamBoxCount = grp.Sum(s => s.BoxCount);
                int toplamFlaconCount = grp.Sum(s => s.FlaconCount);
                var enSonSayim = grp.Max(s => s.SayimTarihi);
                int lotSayisi = grp.Count();

                var sayimAyIlk = new DateTime(enSonSayim.Year, enSonSayim.Month, 1);
                var hareketBaslangic = sayimAyIlk.AddMonths(1);

                var gListesi = gonderimByTest.TryGetValue(testId, out var gl) ? gl : new();
                int gonderim = gListesi.Where(x => x.Month >= hareketBaslangic).Sum(x => x.SentCount);

                var kListesi = kullanimByTest.TryGetValue(testId, out var kl) ? kl : new();
                int kullanim = kListesi.Where(x => x.Month >= hareketBaslangic).Sum(x => x.TestCount);

                int hesapStok = toplamTestCount + gonderim - kullanim;

                var buAy = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var son3AyIlk = buAy.AddMonths(-3);
                var son3AyKullanim = kListesi
                    .Where(x => x.Month >= son3AyIlk && x.Month < buAy)
                    .Sum(x => x.TestCount);
                decimal ortalamaAylik = son3AyKullanim / 3m;

                int runwayGun = ortalamaAylik > 0
                    ? (int)(hesapStok / (ortalamaAylik / 30m))
                    : (hesapStok > 0 ? 9999 : 0);

                int hedefStok = (int)Math.Round(ortalamaAylik * hedefRunwayAy);
                int onerilenTest = Math.Max(0, hedefStok - hesapStok);

                // ★ CHANGED: boxText — Tests.KutuBasinaAdet öncelikli
                //    Snapshot.BoxTextCount sadece Tests boşsa fallback
                int boxText;
                bool kutuBirimli;
                if (testInfo != null
                    && testInfo.GirisTipi == TestGirisTipi.Kutu
                    && testInfo.KutuBasinaAdet.HasValue
                    && testInfo.KutuBasinaAdet.Value > 0)
                {
                    boxText = testInfo.KutuBasinaAdet.Value;
                    kutuBirimli = true;
                }
                else if (testInfo != null && testInfo.GirisTipi == TestGirisTipi.Adet)
                {
                    boxText = 1;
                    kutuBirimli = false;  // adet birimli — kutu kavramı yok
                }
                else if (snap.BoxTextCount > 0)
                {
                    boxText = snap.BoxTextCount;
                    kutuBirimli = true;  // snapshot'tan geldiyse kutuluk varsayılıyor
                }
                else
                {
                    boxText = 1;
                    kutuBirimli = false;
                }

                // Önerilen kutu — adet birimli testler için 0 (anlamsız)
                int onerilenKutu = 0;
                if (kutuBirimli && onerilenTest > 0 && boxText > 1)
                {
                    onerilenKutu = (int)Math.Ceiling((double)onerilenTest / boxText);
                }

                var durum = KararVer(hesapStok, ortalamaAylik, runwayGun);

                var uyarilar = new List<string>();
                if (hesapStok < 0)
                    uyarilar.Add($"Hesap stok negatif ({hesapStok}) — kullanım verisi veya sayım yanlış olabilir.");
                if (ortalamaAylik == 0 && hesapStok > 0)
                    uyarilar.Add("Son 3 ayda hiç kullanım yok — ürün durgun olabilir.");
                if ((DateTime.Now - enSonSayim).TotalDays > 120)
                    uyarilar.Add($"Son sayım {(int)(DateTime.Now - enSonSayim).TotalDays} gün önce — yeni sayım gerekli olabilir.");
                if (lotSayisi > 1)
                    uyarilar.Add($"{lotSayisi} farklı lot var — toplam stok gösteriliyor.");

                var satir = new StokHesapSatirVm
                {
                    TestID = testId,
                    TestAdi = testInfo?.TestName ?? snap.ProductName,
                    ReferenceNumber = snap.ReferenceNumber,
                    ProductName = snap.ProductName,
                    Platform = snap.Platform,
                    UrunTipi = snap.UrunTipi,

                    SonSayimTarihi = enSonSayim,
                    SonSayimNo = snap.SayimNo,
                    SonSayimTestCount = toplamTestCount,

                    SayimdanSonraGonderim = gonderim,
                    SayimdanSonraKullanim = kullanim,
                    HesapGuncelStok = hesapStok,

                    Son3AyKullanim = son3AyKullanim,
                    OrtalamaAylikKullanim = Math.Round(ortalamaAylik, 1),
                    RunwayGun = runwayGun,

                    HedefStok = hedefStok,
                    OnerilenTest = onerilenTest,
                    OnerilenKutu = onerilenKutu,
                    BoxTextCount = boxText,

                    Durum = durum,
                    HesaplamaOzeti = $"{toplamTestCount} + {gonderim} − {kullanim} = {hesapStok} test"
                                   + (lotSayisi > 1 ? $" ({lotSayisi} lot)" : "")
                                   + $" · 3 ay ort {ortalamaAylik:N1}/ay · "
                                   + (runwayGun >= 9999 ? "sınırsız" : $"~{runwayGun} gün yetecek"),
                    Uyarilar = uyarilar
                };

                panel.Satirlar.Add(satir);
            }

            // 6) KPI
            panel.ToplamUrun = panel.Satirlar.Count;
            panel.KritikSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Kritik);
            panel.DusukSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Dusuk);
            panel.IzlemedeSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Izlemede);
            panel.SaglikliSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Saglikli);
            panel.VeriYokSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.VeriYok || s.Durum == StokDurumu.Durgun);

            panel.Satirlar = panel.Satirlar
                .OrderBy(s => (int)s.Durum)
                .ThenByDescending(s => s.OnerilenTest)
                .ToList();

            return panel;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK ÜRÜN
        // ═══════════════════════════════════════════════════════════
        public async Task<StokHesapSatirVm?> HesaplaUrunAsync(
            int hospitalId,
            int testId,
            int hedefRunwayAy = 3,
            CancellationToken ct = default)
        {
            var panel = await HesaplaHastaneAsync(hospitalId, hedefRunwayAy, ct);
            return panel?.Satirlar.FirstOrDefault(s => s.TestID == testId);
        }

        // ═══════════════════════════════════════════════════════════
        //  Durum kararı
        // ═══════════════════════════════════════════════════════════
        private static StokDurumu KararVer(int hesapStok, decimal ortalamaAylik, int runwayGun)
        {
            if (ortalamaAylik == 0)
                return hesapStok > 0 ? StokDurumu.Durgun : StokDurumu.VeriYok;

            if (hesapStok <= 0) return StokDurumu.Kritik;
            if (runwayGun < 30) return StokDurumu.Kritik;
            if (runwayGun < 60) return StokDurumu.Dusuk;
            if (runwayGun < 90) return StokDurumu.Izlemede;
            return StokDurumu.Saglikli;
        }
    }
}
