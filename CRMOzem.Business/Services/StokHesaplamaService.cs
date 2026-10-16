using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
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

            // 2) Bu hastanenin AKTİF sayımındaki REAKTİF snapshot'ları (MappedTestID dolu)
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
                return panel; // boş panel
            }

            // Snapshot'tan sayım meta
            panel.SonSayimNo = snapshots.First().SayimNo;
            panel.SonSayimTarihi = snapshots.Max(s => s.SayimTarihi);

            // 3) TestID listesi + ürün bilgisi
            var testIdler = snapshots.Select(s => s.MappedTestID!.Value).Distinct().ToList();

            var testler = await _db.Tests
                .AsNoTracking()
                .Where(t => testIdler.Contains(t.TestID))
                .Select(t => new { t.TestID, t.TestName, t.SutCode })
                .ToListAsync(ct);
            var testById = testler.ToDictionary(t => t.TestID);

            // 4) İlgili gönderim & kullanım datasını BİR DEFA çek
            // Son 6 ay geriye kadar yükle (bugünkü hesap + 3 ay ort için yeterli)
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

            // Dictionary'ye grupla — her TestID için performanslı erişim
            var gonderimByTest = gonderimler
                .GroupBy(x => x.TestID)
                .ToDictionary(g => g.Key, g => g.ToList());
            var kullanimByTest = kullanimlar
                .GroupBy(x => x.TestID)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 5) Her snapshot için satır üret
            foreach (var snap in snapshots.OrderBy(s => s.ProductName))
            {
                var testId = snap.MappedTestID!.Value;
                var testInfo = testById.TryGetValue(testId, out var t) ? t : null;
                var sayimAyIlk = new DateTime(snap.SayimTarihi.Year, snap.SayimTarihi.Month, 1);
                var hareketBaslangic = sayimAyIlk.AddMonths(1);  // sayım ayından sonraki ay

                // Gönderim (sayım ayından sonra)
                var gListesi = gonderimByTest.TryGetValue(testId, out var gl) ? gl : new();
                int gonderim = gListesi.Where(x => x.Month >= hareketBaslangic).Sum(x => x.SentCount);

                // Kullanım (sayım ayından sonra)
                var kListesi = kullanimByTest.TryGetValue(testId, out var kl) ? kl : new();
                int kullanim = kListesi.Where(x => x.Month >= hareketBaslangic).Sum(x => x.TestCount);

                // Hesap stok
                int hesapStok = snap.TestCount + gonderim - kullanim;

                // Son 3 ay ortalama
                var buAy = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var son3AyIlk = buAy.AddMonths(-3);
                var son3AyKullanim = kListesi
                    .Where(x => x.Month >= son3AyIlk && x.Month < buAy)
                    .Sum(x => x.TestCount);
                decimal ortalamaAylik = son3AyKullanim / 3m;

                // Runway
                int runwayGun = ortalamaAylik > 0
                    ? (int)(hesapStok / (ortalamaAylik / 30m))
                    : (hesapStok > 0 ? 9999 : 0);

                // Hedef + öneri
                int hedefStok = (int)Math.Round(ortalamaAylik * hedefRunwayAy);
                int onerilenTest = Math.Max(0, hedefStok - hesapStok);
                int boxText = snap.BoxTextCount > 0 ? snap.BoxTextCount : 1;
                int onerilenKutu = onerilenTest > 0
                    ? (int)Math.Ceiling((double)onerilenTest / boxText)
                    : 0;

                // Durum
                var durum = KararVer(hesapStok, ortalamaAylik, runwayGun);

                // Uyarılar
                var uyarilar = new List<string>();
                if (hesapStok < 0)
                    uyarilar.Add($"Hesap stok negatif ({hesapStok}) — kullanım verisi veya sayım yanlış olabilir.");
                if (ortalamaAylik == 0 && hesapStok > 0)
                    uyarilar.Add("Son 3 ayda hiç kullanım yok — ürün durgun olabilir.");
                if ((DateTime.Now - snap.SayimTarihi).TotalDays > 120)
                    uyarilar.Add($"Son sayım {(int)(DateTime.Now - snap.SayimTarihi).TotalDays} gün önce — yeni sayım gerekli olabilir.");

                var satir = new StokHesapSatirVm
                {
                    TestID = testId,
                    TestAdi = testInfo?.TestName ?? snap.ProductName,
                    ReferenceNumber = snap.ReferenceNumber,
                    ProductName = snap.ProductName,
                    Platform = snap.Platform,
                    UrunTipi = snap.UrunTipi,

                    SonSayimTarihi = snap.SayimTarihi,
                    SonSayimNo = snap.SayimNo,
                    SonSayimTestCount = snap.TestCount,

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
                    HesaplamaOzeti = $"{snap.TestCount} + {gonderim} − {kullanim} = {hesapStok} test · "
                                   + $"3 ay ort {ortalamaAylik:N1}/ay · "
                                   + (runwayGun >= 9999 ? "sınırsız" : $"~{runwayGun} gün yetecek"),
                    Uyarilar = uyarilar
                };

                panel.Satirlar.Add(satir);
            }

            // 6) KPI sayaçları
            panel.ToplamUrun = panel.Satirlar.Count;
            panel.KritikSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Kritik);
            panel.DusukSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Dusuk);
            panel.IzlemedeSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Izlemede);
            panel.SaglikliSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.Saglikli);
            panel.VeriYokSayisi = panel.Satirlar.Count(s => s.Durum == StokDurumu.VeriYok || s.Durum == StokDurumu.Durgun);

            // Önerilen miktara göre sırala (yüksek öncelik üstte)
            panel.Satirlar = panel.Satirlar
                .OrderBy(s => (int)s.Durum)         // Kritik önce
                .ThenByDescending(s => s.OnerilenTest)
                .ToList();

            return panel;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK ÜRÜN HESAPLAMA (opsiyonel)
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
