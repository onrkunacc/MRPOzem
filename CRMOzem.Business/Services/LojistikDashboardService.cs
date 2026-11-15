using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class LojistikDashboardService : ILojistikDashboardService
    {
        private readonly ApplicationDbContext _db;

        public LojistikDashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<LojistikDashboardVm> GetDashboardAsync(CancellationToken ct)
        {
            var vm = new LojistikDashboardVm();

            // Hepsini paralel yapmak yerine sırayla (DbContext thread-safe değil)
            vm.Kpi = await BuildKpiAsync(ct);
            vm.Hastaneler = await BuildHastaneSagligiAsync(ct);
            vm.Uyarilar = await BuildUyarilarAsync(ct);
            vm.SonAktiviteler = await BuildSonAktivitelerAsync(ct);

            return vm;
        }

        // ═══════════════════════════════════════════════════════════
        //  KPI
        // ═══════════════════════════════════════════════════════════
        private async Task<DashboardKpiVm> BuildKpiAsync(CancellationToken ct)
        {
            var buHaftaBas = DateTime.Now.AddDays(-7);
            var son30Gun = DateTime.Now.AddDays(-30);

            // Tek sorguda yeterli — talepleri + kalemleri çek
            var tumTalepler = await _db.SiparisTalepler
                .AsNoTracking()
                .Include(t => t.Kalemler)
                .ToListAsync(ct);

            var bekleyen = tumTalepler
                .Where(t => t.Durum == SiparisTalepDurumu.Beklemede)
                .ToList();

            var kritikKalemBekliyor = bekleyen
                .Count(t => t.Kalemler.Any(k => k.DogrulamaRengi == DogrulamaRengi.Kirmizi));

            var buHaftaOnay = tumTalepler.Count(t =>
                (t.Durum == SiparisTalepDurumu.OnaylandiAynen ||
                 t.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi)
                && t.OnayTarihi.HasValue && t.OnayTarihi >= buHaftaBas);

            var buHaftaRed = tumTalepler.Count(t =>
                t.Durum == SiparisTalepDurumu.Reddedildi
                && t.OnayTarihi.HasValue && t.OnayTarihi >= buHaftaBas);

            var aktifHastane = tumTalepler
                .Where(t => t.TalepTarihi >= son30Gun)
                .Select(t => t.HospitalID)
                .Distinct()
                .Count();

            var son30GunKalemler = tumTalepler
                .Where(t => t.OnayTarihi.HasValue && t.OnayTarihi >= son30Gun
                         && (t.Durum == SiparisTalepDurumu.OnaylandiAynen
                          || t.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi))
                .SelectMany(t => t.Kalemler)
                .ToList();

            var son30GunToplam = son30GunKalemler.Sum(k => k.OnaylananTest ?? 0);
            var son30GunKutu = son30GunKalemler.Sum(OnaylananKutu);

            return new DashboardKpiVm
            {
                BekleyenTalep = bekleyen.Count,
                KritikKalemBekliyor = kritikKalemBekliyor,
                BuHaftaOnaylanan = buHaftaOnay,
                BuHaftaReddedilen = buHaftaRed,
                AktifHastaneSayisi = aktifHastane,
                Son30GunToplamOnayTest = son30GunToplam,
                Son30GunToplamOnayKutu = Math.Round(son30GunKutu, 0)
            };
        }

        /// <summary>
        /// Onaylanan miktarı sipariş birimine (kutu / flakon / adet) çevirir.
        /// Onay test cinsinden saklanır; hastanenin istediği birim, talepteki
        /// test ÷ kutu oranından bulunur.
        /// </summary>
        private static decimal OnaylananKutu(SiparisTalepKalem k)
        {
            var onay = k.OnaylananTest ?? 0;
            if (onay <= 0) return 0;

            if (k.HastaneTalepKutu is > 0 && k.HastaneTalepTest > 0)
                return onay / ((decimal)k.HastaneTalepTest / k.HastaneTalepKutu.Value);

            return onay;
        }

        // ═══════════════════════════════════════════════════════════
        //  HASTANE SAĞLIĞI
        // ═══════════════════════════════════════════════════════════
        private async Task<List<HastaneSagligiSatirVm>> BuildHastaneSagligiAsync(CancellationToken ct)
        {
            // Aktif eşleşmiş hastaneler
            var aktifMappings = await _db.HospitalEslesmeleri
                .AsNoTracking()
                .Where(e => !e.Yoksay && e.InternalHospitalID.HasValue)
                .Select(e => e.InternalHospitalID!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (aktifMappings.Count == 0) return new List<HastaneSagligiSatirVm>();

            var hastaneler = await _db.Hospitals
                .AsNoTracking()
                .Where(h => aktifMappings.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            // Son sayım tarihi (hastane başına)
            var sonSayimRaw = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => aktifMappings.Contains(s.HospitalID) && s.SayimAktifMi)
                .GroupBy(s => s.HospitalID)
                .Select(g => new { HospitalID = g.Key, MaxSayim = g.Max(s => s.SayimTarihi) })
                .ToListAsync(ct);
            var sonSayimlar = sonSayimRaw.ToDictionary(x => x.HospitalID, x => (DateTime?)x.MaxSayim);

            // Bekleyen talepler + kalemler
            var bekleyenler = await _db.SiparisTalepler
                .AsNoTracking()
                .Include(t => t.Kalemler)
                .Where(t => aktifMappings.Contains(t.HospitalID)
                         && t.Durum == SiparisTalepDurumu.Beklemede)
                .ToListAsync(ct);
            var bekleyenByHospital = bekleyenler
                .GroupBy(t => t.HospitalID)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Son onay tarihi
            var sonOnayRaw = await _db.SiparisTalepler
                .AsNoTracking()
                .Where(t => aktifMappings.Contains(t.HospitalID)
                         && t.OnayTarihi.HasValue
                         && (t.Durum == SiparisTalepDurumu.OnaylandiAynen
                          || t.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi))
                .GroupBy(t => t.HospitalID)
                .Select(g => new { HospitalID = g.Key, SonOnay = g.Max(t => t.OnayTarihi!.Value) })
                .ToListAsync(ct);
            var sonOnaylar = sonOnayRaw.ToDictionary(x => x.HospitalID, x => (DateTime?)x.SonOnay);

            var result = new List<HastaneSagligiSatirVm>();
            foreach (var h in hastaneler)
            {
                var sonSayim = sonSayimlar.TryGetValue(h.HospitalID, out var ss) ? ss : null;
                var bekleyenListe = bekleyenByHospital.TryGetValue(h.HospitalID, out var bl)
                    ? bl : new List<SiparisTalep>();
                var sonOnay = sonOnaylar.TryGetValue(h.HospitalID, out var so) ? so : null;

                var tumKalemler = bekleyenListe.SelectMany(t => t.Kalemler).ToList();
                var kritikKalem = tumKalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Kirmizi);
                var sariVar = tumKalemler.Any(k => k.DogrulamaRengi == DogrulamaRengi.Sari);

                var enKotu = DogrulamaRengi.VeriYok;
                if (kritikKalem > 0) enKotu = DogrulamaRengi.Kirmizi;
                else if (sariVar) enKotu = DogrulamaRengi.Sari;
                else if (tumKalemler.Any(k => k.DogrulamaRengi == DogrulamaRengi.Yesil))
                    enKotu = DogrulamaRengi.Yesil;

                // Sağlık durumu
                var gunSayim = sonSayim.HasValue ? (int)(DateTime.Now - sonSayim.Value).TotalDays : 999;
                HastaneSaglikDurumu durum;
                if (gunSayim >= 180 || kritikKalem > 0)
                    durum = HastaneSaglikDurumu.Kritik;
                else if (gunSayim >= 120 || sariVar)
                    durum = HastaneSaglikDurumu.Dikkat;
                else
                    durum = HastaneSaglikDurumu.Normal;

                result.Add(new HastaneSagligiSatirVm
                {
                    HospitalID = h.HospitalID,
                    HospitalName = h.HospitalName,
                    SonSayimTarihi = sonSayim,
                    BekleyenTalepSayisi = bekleyenListe.Count,
                    BekleyenKritikKalemSayisi = kritikKalem,
                    EnKotuRenk = enKotu,
                    SonOnayTarihi = sonOnay,
                    Durum = durum
                });
            }

            return result
                .OrderByDescending(x => (int)x.Durum)
                .ThenByDescending(x => x.BekleyenKritikKalemSayisi)
                .ThenByDescending(x => x.BekleyenTalepSayisi)
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════
        //  VERİ SAĞLIĞI UYARILARI
        // ═══════════════════════════════════════════════════════════
        private async Task<List<VeriSagligiUyarisiVm>> BuildUyarilarAsync(CancellationToken ct)
        {
            var uyarilar = new List<VeriSagligiUyarisiVm>();
            var sinir120 = DateTime.Now.AddDays(-120);

            // Aktif hastaneler
            var aktifMappings = await _db.HospitalEslesmeleri
                .AsNoTracking()
                .Where(e => !e.Yoksay && e.InternalHospitalID.HasValue)
                .Select(e => e.InternalHospitalID!.Value)
                .Distinct()
                .ToListAsync(ct);

            // 1. Eski sayım & sayım yok
            var sonSayimlar = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => aktifMappings.Contains(s.HospitalID) && s.SayimAktifMi)
                .GroupBy(s => s.HospitalID)
                .Select(g => new { HospitalID = g.Key, MaxSayim = g.Max(s => s.SayimTarihi) })
                .ToListAsync(ct);

            var sayimYapilmisIds = sonSayimlar.Select(s => s.HospitalID).ToHashSet();
            var sayimYokSayi = aktifMappings.Count(id => !sayimYapilmisIds.Contains(id));
            var eskiSayimSayi = sonSayimlar.Count(s => s.MaxSayim < sinir120);

            if (sayimYokSayi > 0)
            {
                uyarilar.Add(new VeriSagligiUyarisiVm
                {
                    Tur = UyariTuru.SayimYok,
                    Baslik = $"Sipariş veren {sayimYokSayi} hastanenin sayımı bize düşmemiş",
                    Aciklama = "Sayım yapılmamış ya da sayım uygulamasındaki hastane adı eşleşmiyor. Stok tahminle hesaplanıyor.",
                    Adet = sayimYokSayi,
                    AksiyonUrl = "/Lojistik/Dashboard/SayimEslesme",
                    AksiyonText = "Sayım eşleşmesi",
                    Seviye = "danger"
                });
            }

            if (eskiSayimSayi > 0)
            {
                uyarilar.Add(new VeriSagligiUyarisiVm
                {
                    Tur = UyariTuru.EskiSayim,
                    Baslik = $"{eskiSayimSayi} hastanenin sayımı güncel değil",
                    Aciklama = "Son fiziki sayım 120 günden eski. Stok tahmini zayıflıyor; yeni sayım planlanmalı.",
                    Adet = eskiSayimSayi,
                    AksiyonUrl = "/Lojistik/Dashboard/SaglikRaporu",
                    AksiyonText = "Hangileri?",
                    Seviye = "warn"
                });
            }

            // 2. Negatif stok
            var negatifStokSayisi = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .CountAsync(k => k.HesapGuncelStok < 0
                              && k.SiparisTalep!.Durum == SiparisTalepDurumu.Beklemede, ct);

            if (negatifStokSayisi > 0)
            {
                uyarilar.Add(new VeriSagligiUyarisiVm
                {
                    Tur = UyariTuru.NegatifStok,
                    Baslik = $"Bekleyen {negatifStokSayisi} kalemde hesaplanan stok eksi çıkıyor",
                    Aciklama = "Bildirilen kullanım, sayım + gönderimden fazla. Gönderim kaydı eksik ya da kullanım fazla girilmiş olabilir.",
                    Adet = negatifStokSayisi,
                    AksiyonUrl = "/Lojistik/Dashboard/SaglikRaporu",
                    AksiyonText = "Hangileri?",
                    Seviye = "danger"
                });
            }

            // 3. Eşleşmeyen reaktif (snapshot var, MappedTestID yok)
            // Aynı ürün her hastanede ve her lotta ayrı satırdır; satır değil ÜRÜN sayılır.
            var eslesmeyenTest = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => s.SayimAktifMi
                         && s.UrunTipi == UrunTipi.Reaktif
                         && !s.MappedTestID.HasValue)
                .Select(s => s.ReferenceNumber)
                .Distinct()
                .CountAsync(ct);

            if (eslesmeyenTest > 0)
            {
                uyarilar.Add(new VeriSagligiUyarisiVm
                {
                    Tur = UyariTuru.EslesmeyeTest,
                    Baslik = $"Sayımdaki {eslesmeyenTest} reaktif ürün katalogda bulunamadı",
                    Aciklama = "Referans numarası Testler listesinde yok. Bu ürünlerin sayımı stok hesabına girmiyor.",
                    Adet = eslesmeyenTest,
                    AksiyonUrl = "/Lojistik/Dashboard/SaglikRaporu",
                    AksiyonText = "Hangileri?",
                    Seviye = "warn"
                });
            }

            // 4. Bekleyen siparişlerde katalogla eşleşmeyen ürün
            var eslesmeyenKalem = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .CountAsync(k => !k.TestID.HasValue
                              && k.SiparisTalep!.Durum == SiparisTalepDurumu.Beklemede, ct);

            if (eslesmeyenKalem > 0)
            {
                uyarilar.Add(new VeriSagligiUyarisiVm
                {
                    Tur = UyariTuru.EslesmeyeTest,
                    Baslik = $"Bekleyen siparişlerde {eslesmeyenKalem} kalem katalogla eşleşmedi",
                    Aciklama = "Bu kalemler için öneri hesaplanamıyor. Ürünün referans numarası Testler listesine eklenmeli.",
                    Adet = eslesmeyenKalem,
                    AksiyonUrl = "/Lojistik/Dogrulama",
                    AksiyonText = "Doğrulamada gör",
                    Seviye = "warn"
                });
            }

            return uyarilar;
        }

        // ═══════════════════════════════════════════════════════════
        //  SON AKTİVİTELER
        // ═══════════════════════════════════════════════════════════
        private async Task<List<SonAktiviteVm>> BuildSonAktivitelerAsync(CancellationToken ct)
        {
            var son = await _db.SiparisTalepler
                .AsNoTracking()
                .Include(t => t.Hospital)
                .Include(t => t.Kalemler)
                .Where(t => t.Durum != SiparisTalepDurumu.Beklemede && t.OnayTarihi.HasValue)
                .OrderByDescending(t => t.OnayTarihi)
                .Take(10)
                .ToListAsync(ct);

            return son.Select(t => new SonAktiviteVm
            {
                TalepID = t.ID,
                HospitalName = t.Hospital?.HospitalName ?? "—",
                Durum = t.Durum,
                OnayTarihi = t.OnayTarihi,
                Kullanici = t.OnaylayanKullanici,
                Not = t.OnayNotu,
                KalemSayisi = t.Kalemler.Count,
                ToplamOnaylananTest = t.Kalemler.Sum(k => k.OnaylananTest ?? 0),
                ToplamOnaylananKutu = Math.Round(t.Kalemler.Sum(OnaylananKutu), 0)
            }).ToList();
        }
    }
}
