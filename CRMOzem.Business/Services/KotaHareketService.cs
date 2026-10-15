using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class KotaHareketService : IKotaHareketService
    {
        private readonly ApplicationDbContext _db;
        private readonly ITcmbKurService _kurService;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public KotaHareketService(ApplicationDbContext db, ITcmbKurService kurService)
        {
            _db = db;
            _kurService = kurService;
        }

        public async Task<KotaDetayVm?> GetDetayAsync(int kotaId, CancellationToken ct = default)
        {
            var kota = await _db.Kotalar.AsNoTracking()
                .FirstOrDefaultAsync(k => k.KotaID == kotaId, ct);
            if (kota == null) return null;

            var hareketler = await _db.KotaHareketleri.AsNoTracking()
                .Include(h => h.Tedarikci)
                .Include(h => h.Hospital)
                .Where(h => h.KotaID == kotaId)
                .OrderByDescending(h => h.Yil).ThenByDescending(h => h.Ay)
                .ThenByDescending(h => h.FaturaTarihi)
                .Select(h => new KotaHareketRowVm
                {
                    KotaHareketID = h.KotaHareketID,
                    HareketTuru = h.HareketTuru,
                    TedarikciID = h.TedarikciID,
                    TedarikciAd = h.Tedarikci!.Ad,
                    TedarikciGrup = h.Tedarikci.Grup,
                    HospitalID = h.HospitalID,
                    HospitalName = h.Hospital != null ? h.Hospital.HospitalName : null,
                    Yil = h.Yil,
                    Ay = h.Ay,
                    FaturaNo = h.FaturaNo,
                    FaturaTarihi = h.FaturaTarihi,
                    Aciklama = h.Aciklama,
                    Tutar = h.Tutar,
                    ParaBirimi = h.ParaBirimi,
                    Kur = h.Kur,
                    TLKarsiligi = h.TLKarsiligi,
                    UsdKarsiligi = h.UsdKarsiligi
                })
                .ToListAsync(ct);

            var vm = new KotaDetayVm
            {
                KotaID = kota.KotaID,
                KotaKodu = kota.KotaKodu,
                IsAdi = kota.IsAdi,
                Bolge = kota.Bolge,
                Sorumlu = kota.Sorumlu,
                Aciklama = kota.Aciklama,
                ProformaTutarUsd = kota.ProformaTutarUsd,
                ProformaTarihi = kota.ProformaTarihi,
                BaslamaTarihi = kota.BaslamaTarihi,
                BitisTarihi = kota.BitisTarihi,
                AySayisi = kota.AySayisi,
                AylikTutarUsd = kota.AylikTutarUsd,
                Durum = kota.Durum,
                Hareketler = hareketler
            };

            // Toplamlar
            var giderler = hareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gider).ToList();
            var gelirler = hareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gelir).ToList();

            vm.ToplamGiderUsd = giderler.Sum(h => h.UsdKarsiligi);
            vm.ToplamGiderTry = giderler.Sum(h => h.TLKarsiligi);
            vm.ToplamGelirUsd = gelirler.Sum(h => h.UsdKarsiligi);
            vm.ToplamGelirTry = gelirler.Sum(h => h.TLKarsiligi);

            vm.AbbottGiderUsd = giderler
                .Where(h => h.TedarikciGrup == TedarikciGrup.Abbott)
                .Sum(h => h.UsdKarsiligi);
            vm.ThirdPartyGiderUsd = giderler
                .Where(h => h.TedarikciGrup == TedarikciGrup.ThirdParty)
                .Sum(h => h.UsdKarsiligi);
            vm.DigerGiderUsd = giderler
                .Where(h => h.TedarikciGrup != TedarikciGrup.Abbott && h.TedarikciGrup != TedarikciGrup.ThirdParty)
                .Sum(h => h.UsdKarsiligi);

            // Aylık özet
            vm.AylikOzet = hareketler
                .GroupBy(h => new { h.Yil, h.Ay })
                .OrderByDescending(g => g.Key.Yil).ThenByDescending(g => g.Key.Ay)
                .Select(g =>
                {
                    var giderlerAy = g.Where(h => h.HareketTuru == KotaHareketTuru.Gider).ToList();
                    var gelirlerAy = g.Where(h => h.HareketTuru == KotaHareketTuru.Gelir).ToList();
                    var ayTarihi = new DateTime(g.Key.Yil, g.Key.Ay, 1);

                    return new AylikOzetRowVm
                    {
                        Yil = g.Key.Yil,
                        Ay = g.Key.Ay,
                        AyLabel = ayTarihi.ToString("MMMM yyyy", TR),
                        AbbottGiderUsd = giderlerAy.Where(h => h.TedarikciGrup == TedarikciGrup.Abbott).Sum(h => h.UsdKarsiligi),
                        ThirdPartyGiderUsd = giderlerAy.Where(h => h.TedarikciGrup == TedarikciGrup.ThirdParty).Sum(h => h.UsdKarsiligi),
                        DigerGiderUsd = giderlerAy.Where(h => h.TedarikciGrup != TedarikciGrup.Abbott && h.TedarikciGrup != TedarikciGrup.ThirdParty).Sum(h => h.UsdKarsiligi),
                        ToplamGiderUsd = giderlerAy.Sum(h => h.UsdKarsiligi),
                        GelirUsd = gelirlerAy.Sum(h => h.UsdKarsiligi)
                    };
                })
                .ToList();
            

            //Hastane Bazlı Özet
            vm.HastaneOzet = hareketler
                .Where(h => h.HospitalID.HasValue)
                .GroupBy(h => new {h.HospitalID,h.HospitalName})
                .Select(g => new KotaHospitalOzetVm
                {
                    HospitalID = g.Key.HospitalID!.Value,
                    HospitalName = g.Key.HospitalName ?? "-",
                    HareketSayisi = g.Count(),
                    ToplamGiderUsd = g.Where(h => h.HareketTuru == KotaHareketTuru.Gider).Sum(h => h.UsdKarsiligi),
                    ToplamGelirUsd = g.Where(h => h.HareketTuru == KotaHareketTuru.Gelir).Sum(h => h.UsdKarsiligi),
                    AbbottGiderUsd = g.Where(h => h.HareketTuru == KotaHareketTuru.Gider && h.TedarikciGrup == TedarikciGrup.Abbott).Sum(h => h.UsdKarsiligi),
                    ThirdPartyGiderUsd = g.Where(h => h.HareketTuru == KotaHareketTuru.Gider && h.TedarikciGrup == TedarikciGrup.ThirdParty).Sum(h => h.UsdKarsiligi)
                })
                .OrderByDescending(h => h.ToplamGiderUsd)
                .ToList();

            return vm;
        }

        public async Task<KotaHareketEditVm?> GetHareketAsync(int hareketId, CancellationToken ct = default)
        {
            return await _db.KotaHareketleri.AsNoTracking()
                .Where(h => h.KotaHareketID == hareketId)
                .Select(h => new KotaHareketEditVm
                {
                    KotaHareketID = h.KotaHareketID,
                    KotaID = h.KotaID,
                    HareketTuru = h.HareketTuru,
                    TedarikciID = h.TedarikciID,
                    HospitalID = h.HospitalID,
                    FaturaTarihi = h.FaturaTarihi,
                    FaturaNo = h.FaturaNo,
                    Aciklama = h.Aciklama,
                    Tutar = h.Tutar,
                    ParaBirimi = h.ParaBirimi,
                    Kur = h.Kur
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<KotaHareket> CreateAsync(KotaHareketCreateVm vm, string? userId, CancellationToken ct = default)
        {
            var (kur, tl, usd) = await HesaplaAsync(vm.ParaBirimi, vm.Tutar, vm.FaturaTarihi, vm.Kur, ct);

            var faturaTarih = vm.FaturaTarihi ?? DateTime.Today;

            var entity = new KotaHareket
            {
                KotaID = vm.KotaID,
                TedarikciID = vm.TedarikciID,
                HospitalID = vm.HospitalID,
                HareketTuru = vm.HareketTuru,
                Yil = faturaTarih.Year,
                Ay = faturaTarih.Month,
                FaturaNo = string.IsNullOrWhiteSpace(vm.FaturaNo) ? null : vm.FaturaNo.Trim(),
                FaturaTarihi = vm.FaturaTarihi,
                Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim(),
                Tutar = vm.Tutar,
                ParaBirimi = vm.ParaBirimi,
                Kur = kur,
                TLKarsiligi = tl,
                UsdKarsiligi = usd,
                EkleyenKullaniciId = userId,
                OlusturmaTarihi = DateTime.Now
            };

            _db.KotaHareketleri.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<KotaHareket> UpdateAsync(KotaHareketEditVm vm, CancellationToken ct = default)
        {
            var entity = await _db.KotaHareketleri.FirstOrDefaultAsync(h => h.KotaHareketID == vm.KotaHareketID, ct)
                ?? throw new InvalidOperationException("Hareket bulunamadı.");

            var (kur, tl, usd) = await HesaplaAsync(vm.ParaBirimi, vm.Tutar, vm.FaturaTarihi, vm.Kur, ct);

            var faturaTarih = vm.FaturaTarihi ?? DateTime.Today;

            entity.TedarikciID = vm.TedarikciID;
            entity.HospitalID = vm.HospitalID;
            entity.HareketTuru = vm.HareketTuru;
            entity.Yil = faturaTarih.Year;
            entity.Ay = faturaTarih.Month;
            entity.FaturaNo = string.IsNullOrWhiteSpace(vm.FaturaNo) ? null : vm.FaturaNo.Trim();
            entity.FaturaTarihi = vm.FaturaTarihi;
            entity.Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim();
            entity.Tutar = vm.Tutar;
            entity.ParaBirimi = vm.ParaBirimi;
            entity.Kur = kur;
            entity.TLKarsiligi = tl;
            entity.UsdKarsiligi = usd;
            entity.GuncellemeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<bool> DeleteAsync(int hareketId, CancellationToken ct = default)
        {
            var entity = await _db.KotaHareketleri.FirstOrDefaultAsync(h => h.KotaHareketID == hareketId, ct);
            if (entity == null) return false;

            _db.KotaHareketleri.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }


        private async Task<(decimal kur, decimal tl, decimal usd)> HesaplaAsync(
            string paraBirimi, decimal tutar, DateTime? tarih, decimal? manuelKur, CancellationToken ct)
        {
            var pb = paraBirimi?.Trim().ToUpperInvariant() ?? "TRY";
            var faturaTarih = tarih ?? DateTime.Today;

            if (pb == "TRY")
            {
                // TL için kur = 1 → dönüşüm için TCMB'den al
                var kurBilgi = await _kurService.GetKurAsync(faturaTarih, ct);
                var usdKur = manuelKur ?? kurBilgi?.UsdSatis ?? 0;

                var usdKarsiligi = usdKur > 0 ? Math.Round(tutar / usdKur, 2) : 0;
                return (usdKur, tutar, usdKarsiligi);
            }
            else if (pb == "USD")
            {
                var kurBilgi = await _kurService.GetKurAsync(faturaTarih, ct);
                var kur = manuelKur ?? kurBilgi?.UsdSatis ?? 0;

                if (kur <= 0)
                    throw new InvalidOperationException($"TCMB'den {faturaTarih:dd.MM.yyyy} tarihine ait USD kuru alınamadı. Manuel kur giriniz.");

                var tlKarsiligi = Math.Round(tutar * kur, 2);
                return (kur, tlKarsiligi, tutar);
            }
            else
            {
                throw new InvalidOperationException($"Desteklenmeyen para birimi: {paraBirimi}");
            }
        }
    }
}