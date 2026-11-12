using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class KotaDashboardService : IKotaDashboardService
    {
        private readonly ApplicationDbContext _db;
        private readonly ITcmbKurService _kurService;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public KotaDashboardService(ApplicationDbContext db, ITcmbKurService kurService)
        {
            _db = db;
            _kurService = kurService;
        }

        public async Task<KotaDashboardVm> BuildAsync(KotaDashboardFilterVm? filter = null, CancellationToken ct = default)
        {
            filter ??= new KotaDashboardFilterVm();

            var vm = new KotaDashboardVm { Filter = filter };
            var bugun = DateTime.Now;
            var buAyIlk = new DateTime(bugun.Year, bugun.Month, 1);
            var gecenAyIlk = buAyIlk.AddMonths(-1);

            // ═══ Dropdown verileri ═══
            vm.MevcutBolgeler = await _db.Kotalar.AsNoTracking()
                .Where(k => k.Bolge != null)
                .Select(k => k.Bolge!)
                .Distinct().OrderBy(x => x)
                .ToListAsync(ct);

            vm.MevcutKotalar = await _db.Kotalar.AsNoTracking()
                .OrderBy(k => k.IsAdi)
                .Select(k => new KotaOptionVm
                {
                    KotaID = k.KotaID,
                    IsAdi = k.IsAdi,
                    KotaKodu = k.KotaKodu
                }).ToListAsync(ct);

            // ═══ Kotalar (filtrelenmiş) ═══
            var kotaQuery = _db.Kotalar.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Bolge))
                kotaQuery = kotaQuery.Where(k => k.Bolge == filter.Bolge);
            if (filter.Durum.HasValue)
                kotaQuery = kotaQuery.Where(k => k.Durum == filter.Durum.Value);
            if (filter.KotaId.HasValue)
                kotaQuery = kotaQuery.Where(k => k.KotaID == filter.KotaId.Value);

            var kotalar = await kotaQuery.ToListAsync(ct);
            var filtrelenmisKotaIdler = kotalar.Select(k => k.KotaID).ToList();

            vm.ToplamKotaSayisi = kotalar.Count;
            vm.AktifKotaSayisi = kotalar.Count(k => k.Durum == KotaDurum.Aktif);
            vm.OnaydaBekleyenSayisi = kotalar.Count(k => k.Durum == KotaDurum.Onayda || k.Durum == KotaDurum.Bekleyen);
            vm.YeniBaslayacakSayisi = kotalar.Count(k => k.Durum == KotaDurum.YeniBaslayacak);
            vm.KapananSayisi = kotalar.Count(k => k.Durum == KotaDurum.Kapandi);
            vm.ToplamProformaUsd = kotalar.Where(k => k.ProformaTutarUsd.HasValue).Sum(k => k.ProformaTutarUsd!.Value);

            // ═══ Hareketler (filtrelenmiş) ═══
            var hareketQuery = _db.KotaHareketleri.AsNoTracking()
                .Include(h => h.Tedarikci)
                .Include(h => h.Kota)
                .Where(h => filtrelenmisKotaIdler.Contains(h.KotaID));

            if (filter.StartDate.HasValue)
                hareketQuery = hareketQuery.Where(h => h.FaturaTarihi >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
            {
                var end = filter.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
                hareketQuery = hareketQuery.Where(h => h.FaturaTarihi <= end);
            }
            if (filter.TedarikciGrup.HasValue)
                hareketQuery = hareketQuery.Where(h => h.Tedarikci!.Grup == filter.TedarikciGrup.Value);

            var tumHareketler = await hareketQuery.ToListAsync(ct);

            var giderler = tumHareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gider).ToList();
            var gelirler = tumHareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gelir).ToList();

            vm.ToplamGiderUsd = giderler.Sum(h => h.UsdKarsiligi);
            vm.ToplamGelirUsd = gelirler.Sum(h => h.UsdKarsiligi);

            vm.AbbottGiderUsd = giderler
                .Where(h => h.Tedarikci?.Grup == TedarikciGrup.Abbott)
                .Sum(h => h.UsdKarsiligi);
            vm.ThirdPartyGiderUsd = giderler
                .Where(h => h.Tedarikci?.Grup == TedarikciGrup.ThirdParty)
                .Sum(h => h.UsdKarsiligi);
            vm.DigerGiderUsd = giderler
                .Where(h => h.Tedarikci?.Grup != TedarikciGrup.Abbott && h.Tedarikci?.Grup != TedarikciGrup.ThirdParty)
                .Sum(h => h.UsdKarsiligi);

            // ═══ Bu ay / Geçen ay ═══
            vm.BuAyGiderUsd = giderler.Where(h => h.Yil == buAyIlk.Year && h.Ay == buAyIlk.Month).Sum(h => h.UsdKarsiligi);
            vm.GecenAyGiderUsd = giderler.Where(h => h.Yil == gecenAyIlk.Year && h.Ay == gecenAyIlk.Month).Sum(h => h.UsdKarsiligi);
            vm.BuAyGelirUsd = gelirler.Where(h => h.Yil == buAyIlk.Year && h.Ay == buAyIlk.Month).Sum(h => h.UsdKarsiligi);
            vm.GecenAyGelirUsd = gelirler.Where(h => h.Yil == gecenAyIlk.Year && h.Ay == gecenAyIlk.Month).Sum(h => h.UsdKarsiligi);

            // ═══ Aylık Trend ═══
            DateTime trendBaslangic;
            DateTime trendBitis;

            if (filter.StartDate.HasValue && filter.EndDate.HasValue)
            {
                trendBaslangic = new DateTime(filter.StartDate.Value.Year, filter.StartDate.Value.Month, 1);
                trendBitis = new DateTime(filter.EndDate.Value.Year, filter.EndDate.Value.Month, 1);
            }
            else
            {
                trendBitis = buAyIlk;
                trendBaslangic = buAyIlk.AddMonths(-11);
            }

            vm.AylikTrend = new List<AylikTrendVm>();
            var currentAy = trendBaslangic;
            while (currentAy <= trendBitis)
            {
                var ayGider = giderler.Where(h => h.Yil == currentAy.Year && h.Ay == currentAy.Month).Sum(h => h.UsdKarsiligi);
                var ayGelir = gelirler.Where(h => h.Yil == currentAy.Year && h.Ay == currentAy.Month).Sum(h => h.UsdKarsiligi);

                vm.AylikTrend.Add(new AylikTrendVm
                {
                    Yil = currentAy.Year,
                    Ay = currentAy.Month,
                    AyLabel = currentAy.ToString("MMM yy", TR),
                    GiderUsd = ayGider,
                    GelirUsd = ayGelir
                });
                currentAy = currentAy.AddMonths(1);
            }

            // ═══ Kota Kullanım ═══
            vm.KotaKullanimlari = kotalar
                .Where(k => k.ProformaTutarUsd.HasValue && k.ProformaTutarUsd.Value > 0)
                .Select(k =>
                {
                    var gider = giderler.Where(h => h.KotaID == k.KotaID).Sum(h => h.UsdKarsiligi);
                    return new KotaKullanimVm
                    {
                        KotaID = k.KotaID,
                        IsAdi = k.IsAdi,
                        KotaKodu = k.KotaKodu,
                        ProformaUsd = k.ProformaTutarUsd!.Value,
                        GiderUsd = gider,
                        Yuzde = Math.Round((gider / k.ProformaTutarUsd.Value) * 100, 1),
                        Durum = k.Durum
                    };
                })
                .OrderByDescending(x => x.Yuzde)
                .Take(10)
                .ToList();

            // ═══ Bölge Özet ═══
            vm.BolgeOzet = kotalar
                .Where(k => !string.IsNullOrWhiteSpace(k.Bolge))
                .GroupBy(k => k.Bolge!)
                .Select(g => new BolgeOzetVm
                {
                    Bolge = g.Key,
                    KotaSayisi = g.Count(),
                    ToplamProformaUsd = g.Where(k => k.ProformaTutarUsd.HasValue).Sum(k => k.ProformaTutarUsd!.Value),
                    ToplamGiderUsd = giderler.Where(h => g.Select(k => k.KotaID).Contains(h.KotaID)).Sum(h => h.UsdKarsiligi)
                })
                .OrderByDescending(b => b.ToplamProformaUsd)
                .ToList();

            // ═══ Yıllık Özet ═══
            var yillar = tumHareketler.Select(h => h.Yil).Distinct().OrderByDescending(y => y).ToList();
            vm.YillikOzet = yillar.Select(y =>
            {
                var yilGiderler = giderler.Where(h => h.Yil == y).ToList();
                return new YillikOzetVm
                {
                    Yil = y,
                    GiderUsd = yilGiderler.Sum(h => h.UsdKarsiligi),
                    GelirUsd = gelirler.Where(h => h.Yil == y).Sum(h => h.UsdKarsiligi),
                    AbbottGiderUsd = yilGiderler.Where(h => h.Tedarikci?.Grup == TedarikciGrup.Abbott).Sum(h => h.UsdKarsiligi),
                    ThirdPartyGiderUsd = yilGiderler.Where(h => h.Tedarikci?.Grup == TedarikciGrup.ThirdParty).Sum(h => h.UsdKarsiligi),
                    DigerGiderUsd = yilGiderler
                        .Where(h => h.Tedarikci?.Grup != TedarikciGrup.Abbott && h.Tedarikci?.Grup != TedarikciGrup.ThirdParty)
                        .Sum(h => h.UsdKarsiligi)
                };
            }).ToList();

            // ═══ En Yüklü Kotalar ═══
            vm.EnYukluKotalar = kotalar
                .Select(k =>
                {
                    var gider = giderler.Where(h => h.KotaID == k.KotaID).Sum(h => h.UsdKarsiligi);
                    return new EnYukluKotaVm
                    {
                        KotaID = k.KotaID,
                        IsAdi = k.IsAdi,
                        KotaKodu = k.KotaKodu,
                        Bolge = k.Bolge,
                        GiderUsd = gider,
                        ProformaUsd = k.ProformaTutarUsd ?? 0,
                        Yuzde = k.ProformaTutarUsd.HasValue && k.ProformaTutarUsd.Value > 0
                            ? Math.Round((gider / k.ProformaTutarUsd.Value) * 100, 1)
                            : 0,
                        Durum = k.Durum
                    };
                })
                .Where(x => x.GiderUsd > 0)
                .OrderByDescending(x => x.GiderUsd)
                .Take(5)
                .ToList();

            // ═══ ★ KOTA SATIRLARI (ana dashboard tablosu) ★ ═══
            vm.KotaSatirlari = kotalar
                .Select(k =>
                {
                    var kGiderler = giderler.Where(h => h.KotaID == k.KotaID).ToList();
                    var kGelirler = gelirler.Where(h => h.KotaID == k.KotaID).ToList();

                    return new KotaRowVm
                    {
                        KotaID = k.KotaID,
                        KotaKodu = k.KotaKodu,
                        IsAdi = k.IsAdi,
                        Sorumlu = k.Sorumlu,
                        Bolge = k.Bolge,
                        ProformaUsd = k.ProformaTutarUsd ?? 0m,
                        Durum = k.Durum,
                        BaslamaTarihi = k.BaslamaTarihi,
                        BitisTarihi = k.BitisTarihi,

                        AbbottGiderUsd = kGiderler
                            .Where(h => h.Tedarikci?.Grup == TedarikciGrup.Abbott)
                            .Sum(h => h.UsdKarsiligi),

                        ThirdPartyGiderUsd = kGiderler
                            .Where(h => h.Tedarikci?.Grup == TedarikciGrup.ThirdParty)
                            .Sum(h => h.UsdKarsiligi),

                        DigerGiderUsd = kGiderler
                            .Where(h => h.Tedarikci?.Grup != TedarikciGrup.Abbott
                                     && h.Tedarikci?.Grup != TedarikciGrup.ThirdParty)
                            .Sum(h => h.UsdKarsiligi),

                        GelirUsd = kGelirler.Sum(h => h.UsdKarsiligi)
                    };
                })
                .OrderByDescending(r => r.ProformaUsd)
                .ToList();

            // ═══ Güncel Kur ═══
            var kur = await _kurService.GetGuncelKurAsync(ct);
            vm.GuncelUsdKur = kur?.UsdSatis;
            vm.GuncelKurTarihi = kur?.Tarih;

            return vm;
        }


        // ═══════════════════════════════════════════════════════════════
        // ★ Modal için — belirli bir kotanın detayı
        // ═══════════════════════════════════════════════════════════════
        public async Task<KotaDetayModalVm?> GetKotaDetayAsync(int kotaId, CancellationToken ct = default)
        {
            var kota = await _db.Kotalar
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.KotaID == kotaId, ct);
            if (kota == null) return null;

            var hareketler = await _db.KotaHareketleri
                .AsNoTracking()
                .Include(h => h.Tedarikci)
                .Where(h => h.KotaID == kotaId)
                .ToListAsync(ct);

            var giderler = hareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gider).ToList();
            var gelirler = hareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gelir).ToList();

            var abbott = giderler
                .Where(h => h.Tedarikci?.Grup == TedarikciGrup.Abbott)
                .Sum(h => h.UsdKarsiligi);
            var thirdParty = giderler
                .Where(h => h.Tedarikci?.Grup == TedarikciGrup.ThirdParty)
                .Sum(h => h.UsdKarsiligi);
            var diger = giderler
                .Where(h => h.Tedarikci?.Grup != TedarikciGrup.Abbott
                         && h.Tedarikci?.Grup != TedarikciGrup.ThirdParty)
                .Sum(h => h.UsdKarsiligi);
            var gelir = gelirler.Sum(h => h.UsdKarsiligi);

            // ── Aylık trend ──────────────────────────────────────────────
            // Pencere, kotanın KENDİ hareket aralığından türetilir.
            // Önceden "bugünden geriye 12 ay" sabitti; hareketleri bu aralığın
            // dışında kalan kotalarda grafik bomboş görünüyordu.
            var aylik = new List<AylikTrendVm>();

            var tarihliler = hareketler
                .Where(h => h.Yil > 0 && h.Ay >= 1 && h.Ay <= 12)
                .ToList();

            DateTime ilkAy, sonAy;

            if (tarihliler.Count > 0)
            {
                ilkAy = tarihliler.Min(h => new DateTime(h.Yil, h.Ay, 1));
                sonAy = tarihliler.Max(h => new DateTime(h.Yil, h.Ay, 1));

                // Çok uzun aralıklarda grafik okunmaz hale gelir — son 36 ayla sınırla
                const int maksAy = 36;
                var toplamAy = ((sonAy.Year - ilkAy.Year) * 12) + sonAy.Month - ilkAy.Month + 1;
                if (toplamAy > maksAy) ilkAy = sonAy.AddMonths(-(maksAy - 1));
            }
            else
            {
                // Hiç tarihli hareket yoksa grafik yerine "henüz hareket yok" görünsün
                return KotaDetayOlustur(kota, abbott, thirdParty, diger, gelir, aylik);
            }

            var cur = ilkAy;
            while (cur <= sonAy)
            {
                var ayGider = giderler.Where(h => h.Yil == cur.Year && h.Ay == cur.Month).Sum(h => h.UsdKarsiligi);
                var ayGelir = gelirler.Where(h => h.Yil == cur.Year && h.Ay == cur.Month).Sum(h => h.UsdKarsiligi);

                aylik.Add(new AylikTrendVm
                {
                    Yil = cur.Year,
                    Ay = cur.Month,
                    AyLabel = cur.ToString("MMM yy", TR),
                    GiderUsd = ayGider,
                    GelirUsd = ayGelir
                });
                cur = cur.AddMonths(1);
            }

            return KotaDetayOlustur(kota, abbott, thirdParty, diger, gelir, aylik);
        }

        /// <summary>Modal VM'ini kurar — hem normal hem "hareket yok" yolundan çağrılır.</summary>
        private static KotaDetayModalVm KotaDetayOlustur(
            Kota kota,
            decimal abbott, decimal thirdParty, decimal diger, decimal gelir,
            List<AylikTrendVm> aylik)
        {
            return new KotaDetayModalVm
            {
                KotaID = kota.KotaID,
                KotaKodu = kota.KotaKodu,
                IsAdi = kota.IsAdi,
                Sorumlu = kota.Sorumlu,
                Bolge = kota.Bolge,
                BaslamaTarihi = kota.BaslamaTarihi,
                BitisTarihi = kota.BitisTarihi,
                ProformaUsd = kota.ProformaTutarUsd ?? 0m,
                AbbottGiderUsd = abbott,
                ThirdPartyGiderUsd = thirdParty,
                DigerGiderUsd = diger,
                GelirUsd = gelir,
                Durum = kota.Durum,
                AylikTrend = aylik
            };
        }

    }
}