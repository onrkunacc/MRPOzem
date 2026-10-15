using CRMOzem.Business.Interfaces;
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
    public class TedarikciService : ITedarikciService
    {
        private readonly ApplicationDbContext _db;

        public TedarikciService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<TedarikciListVm>> GetAllAsync(bool onlyActive = false, CancellationToken ct = default)
        {
            var query = _db.Tedarikciler.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(t => t.Aktif);

            return await query
                .OrderBy(t => t.Sira)
                .ThenBy(t => t.Ad)
                .Select(t => new TedarikciListVm
                {
                    TedarikciID = t.TedarikciID,
                    Ad = t.Ad,
                    Grup = t.Grup,
                    Aciklama = t.Aciklama,
                    Aktif = t.Aktif,
                    Sira = t.Sira,
                    HareketSayisi = t.Hareketler.Count,
                    OlusturmaTarihi = t.OlusturmaTarihi
                })
                .ToListAsync(ct);
        }

        public async Task<TedarikciEditVm?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.Tedarikciler.AsNoTracking()
                .Where(t => t.TedarikciID == id)
                .Select(t => new TedarikciEditVm
                {
                    TedarikciID = t.TedarikciID,
                    Ad = t.Ad,
                    Grup = t.Grup,
                    Aciklama = t.Aciklama,
                    Aktif = t.Aktif,
                    Sira = t.Sira
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<Tedarikci> CreateAsync(TedarikciCreateVm vm, CancellationToken ct = default)
        {
            // Otomatik sıra: mevcut max + 1
            var maxSira = await _db.Tedarikciler.AnyAsync(ct)
                ? await _db.Tedarikciler.MaxAsync(t => t.Sira, ct)
                : 0;

            var entity = new Tedarikci
            {
                Ad = vm.Ad.Trim(),
                Grup = vm.Grup,
                Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim(),
                Aktif = vm.Aktif,
                Sira = maxSira + 1,
                OlusturmaTarihi = DateTime.Now
            };

            _db.Tedarikciler.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<Tedarikci> UpdateAsync(TedarikciEditVm vm, CancellationToken ct = default)
        {
            var entity = await _db.Tedarikciler.FirstOrDefaultAsync(t => t.TedarikciID == vm.TedarikciID, ct)
                ?? throw new InvalidOperationException("Tedarikçi bulunamadı.");

            entity.Ad = vm.Ad.Trim();
            entity.Grup = vm.Grup;
            entity.Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim();
            entity.Aktif = vm.Aktif;
            entity.Sira = vm.Sira;

            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Tedarikciler
                .Include(t => t.Hareketler)
                .FirstOrDefaultAsync(t => t.TedarikciID == id, ct);

            if (entity == null) return false;
            if (entity.Hareketler.Count > 0)
                throw new InvalidOperationException($"Bu tedarikçiye ait {entity.Hareketler.Count} hareket var. Silmek yerine pasifleştirin.");

            _db.Tedarikciler.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ToggleActiveAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Tedarikciler.FirstOrDefaultAsync(t => t.TedarikciID == id, ct);
            if (entity == null) return false;

            entity.Aktif = !entity.Aktif;
            await _db.SaveChangesAsync(ct);
            return entity.Aktif;
        }
    }
}
