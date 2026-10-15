using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Business.Services
{
    public class KotaService : IKotaService
    {
        private readonly ApplicationDbContext _db;
        private readonly ITcmbKurService _kurService;

        public KotaService(ApplicationDbContext db, ITcmbKurService kurService)
        {
            _db = db;
            _kurService = kurService;
        }

        public async Task<List<KotaListVm>> GetListAsync(KotaFilterVm filter, CancellationToken ct = default)
        {
            var query = _db.Kotalar.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(k =>
                    k.IsAdi.Contains(s) ||
                    (k.KotaKodu != null && k.KotaKodu.Contains(s)) ||
                    (k.Sorumlu != null && k.Sorumlu.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Bolge))
                query = query.Where(k => k.Bolge == filter.Bolge);

            if (filter.Durum.HasValue)
                query = query.Where(k => k.Durum == filter.Durum.Value);

            var kotalar = await query
                .OrderBy(k => k.Durum)
                .ThenByDescending(k => k.ProformaTarihi)
                .ThenBy(k => k.IsAdi)
                .Select(k => new
                {
                    Kota = k,
                    ToplamGiderUsd = k.Hareketler
                        .Where(h => h.HareketTuru == KotaHareketTuru.Gider)
                        .Sum(h => (decimal?)h.UsdKarsiligi) ?? 0,
                    ToplamGelirUsd = k.Hareketler
                        .Where(h => h.HareketTuru == KotaHareketTuru.Gelir)
                        .Sum(h => (decimal?)h.UsdKarsiligi) ?? 0,
                    HastaneSayisi = k.Hastaneler.Count 
                })
                .ToListAsync(ct);

            // Güncel kur (TL karşılığı gösterimi için)
            var guncelKur = await _kurService.GetGuncelKurAsync(ct);
            var kurDegeri = guncelKur?.UsdSatis;

            return kotalar.Select(x => new KotaListVm
            {
                KotaID = x.Kota.KotaID,
                KotaKodu = x.Kota.KotaKodu,
                IsAdi = x.Kota.IsAdi,
                Bolge = x.Kota.Bolge,
                ProformaTutarUsd = x.Kota.ProformaTutarUsd,
                ProformaTutarTry = x.Kota.ProformaTutarUsd.HasValue && kurDegeri.HasValue
                    ? Math.Round(x.Kota.ProformaTutarUsd.Value * kurDegeri.Value, 2)
                    : null,
                ProformaTarihi = x.Kota.ProformaTarihi,
                BaslamaTarihi = x.Kota.BaslamaTarihi,
                BitisTarihi = x.Kota.BitisTarihi,
                AySayisi = x.Kota.AySayisi,
                AylikTutarUsd = x.Kota.AylikTutarUsd,
                Durum = x.Kota.Durum,
                Sorumlu = x.Kota.Sorumlu,
                ToplamGiderUsd = x.ToplamGiderUsd,
                ToplamGelirUsd = x.ToplamGelirUsd,
                HastaneSayisi = x.HastaneSayisi
            }).ToList();
        }

        public async Task<KotaEditVm?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.Kotalar.AsNoTracking()
                .Include(k => k.Hastaneler)
                .Where(k => k.KotaID == id)
                .Select(k => new KotaEditVm
                {
                    KotaID = k.KotaID,
                    KotaKodu = k.KotaKodu,
                    IsAdi = k.IsAdi,
                    Bolge = k.Bolge,
                    ProformaTarihi = k.ProformaTarihi,
                    ProformaTutarUsd = k.ProformaTutarUsd,
                    BaslamaTarihi = k.BaslamaTarihi,
                    BitisTarihi = k.BitisTarihi,
                    AySayisi = k.AySayisi,
                    Durum = k.Durum,
                    Sorumlu = k.Sorumlu,
                    Aciklama = k.Aciklama,
                    SecilenHastaneIdler = k.Hastaneler.Select(h => h.HospitalID).ToList()

                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<Kota> CreateAsync(KotaCreateVm vm, string? userId, CancellationToken ct = default)
        {
            var entity = new Kota
            {
                KotaKodu = string.IsNullOrWhiteSpace(vm.KotaKodu) ? null : vm.KotaKodu.Trim(),
                IsAdi = vm.IsAdi.Trim(),
                Bolge = string.IsNullOrWhiteSpace(vm.Bolge) ? null : vm.Bolge.Trim(),
                ProformaTarihi = vm.ProformaTarihi,
                ProformaTutarUsd = vm.ProformaTutarUsd,
                BaslamaTarihi = vm.BaslamaTarihi,
                BitisTarihi = vm.BitisTarihi,
                AySayisi = vm.AySayisi ?? HesaplaAySayisi(vm.BaslamaTarihi, vm.BitisTarihi),
                Durum = vm.Durum,
                Sorumlu = string.IsNullOrWhiteSpace(vm.Sorumlu) ? null : vm.Sorumlu.Trim(),
                Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim(),
                EkleyenKullaniciId = userId,
                OlusturmaTarihi = DateTime.Now
            };

            _db.Kotalar.Add(entity);
            await _db.SaveChangesAsync(ct);

            if (vm.SecilenHastaneIdler.Any())
            {
                foreach (var hospitalId in vm.SecilenHastaneIdler.Distinct())
                {
                    _db.Set<KotaHospital>().Add(new KotaHospital
                    {
                        KotaID = entity.KotaID,
                        HospitalID = hospitalId,
                        EklenmeTarihi = DateTime.Now
                    });
                }
                await _db.SaveChangesAsync(ct);
            }
            return entity;
        }

        public async Task<Kota> UpdateAsync(KotaEditVm vm, CancellationToken ct = default)
        {
            var entity = await _db.Kotalar
                .Include(k => k.Hastaneler)                              // ← KRİTİK EK
                .FirstOrDefaultAsync(k => k.KotaID == vm.KotaID, ct)
                ?? throw new InvalidOperationException("Kota bulunamadı.");

            entity.KotaKodu = string.IsNullOrWhiteSpace(vm.KotaKodu) ? null : vm.KotaKodu.Trim();
            entity.IsAdi = vm.IsAdi.Trim();
            entity.Bolge = string.IsNullOrWhiteSpace(vm.Bolge) ? null : vm.Bolge.Trim();
            entity.ProformaTarihi = vm.ProformaTarihi;
            entity.ProformaTutarUsd = vm.ProformaTutarUsd;
            entity.BaslamaTarihi = vm.BaslamaTarihi;
            entity.BitisTarihi = vm.BitisTarihi;
            entity.AySayisi = vm.AySayisi ?? HesaplaAySayisi(vm.BaslamaTarihi, vm.BitisTarihi);
            entity.Durum = vm.Durum;
            entity.Sorumlu = string.IsNullOrWhiteSpace(vm.Sorumlu) ? null : vm.Sorumlu.Trim();
            entity.Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim();
            entity.GuncellemeTarihi = DateTime.Now;

            // ── HASTANE SENKRON (defensive) ──────────────────────────
            var yeniIdler = (vm.SecilenHastaneIdler ?? new List<int>())
                                      .Where(id => id > 0)
                                      .Distinct()
                                      .ToHashSet();

            var mevcutHastaneler = entity.Hastaneler ?? new List<KotaHospital>();
            var mevcutIdler = mevcutHastaneler.Select(h => h.HospitalID).ToHashSet();

            // Silinecekler (yeni listede olmayanlar)
            var silinecekler = mevcutHastaneler
                .Where(h => !yeniIdler.Contains(h.HospitalID))
                .ToList();
            foreach (var s in silinecekler)
                _db.Set<KotaHospital>().Remove(s);

            // Eklenecekler (yeni ama şu an bağlı olmayanlar)
            var eklenecekIdler = yeniIdler.Except(mevcutIdler).ToList();
            foreach (var hospitalId in eklenecekIdler)
            {
                _db.Set<KotaHospital>().Add(new KotaHospital
                {
                    KotaID = entity.KotaID,
                    HospitalID = hospitalId,
                    EklenmeTarihi = DateTime.Now
                });
            }

            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Kotalar
                .Include(k => k.Hareketler)
                .FirstOrDefaultAsync(k => k.KotaID == id, ct);

            if (entity == null) return false;
            if (entity.Hareketler.Count > 0)
                throw new InvalidOperationException($"Bu kotaya ait {entity.Hareketler.Count} hareket var. Önce hareketleri silin.");

            _db.Kotalar.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<string>> GetBolgelerAsync(CancellationToken ct = default)
        {
            return await _db.Kotalar.AsNoTracking()
                .Where(k => k.Bolge != null)
                .Select(k => k.Bolge!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);
        }

        private static int? HesaplaAySayisi(DateTime? baslama, DateTime? bitis)
        {
            if (!baslama.HasValue || !bitis.HasValue) return null;
            var ay = ((bitis.Value.Year - baslama.Value.Year) * 12) + (bitis.Value.Month - baslama.Value.Month) + 1;
            return ay > 0 ? ay : null;
        }
    }
}