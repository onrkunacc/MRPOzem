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
    public class IhaleService : IIhaleService
    {
        private readonly ApplicationDbContext _db;

        public IhaleService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<Ihale>> GetAllAsync() =>
            await _db.Ihaleler.Where(i => i.Aktif).OrderByDescending(i => i.OlusturmaTarihi).ToListAsync();

        public async Task<Ihale?> GetByIdAsync(int id) =>
            await _db.Ihaleler
                .Include(i => i.PersonelGiderler).ThenInclude(p => p.Birim)
                .Include(i => i.EkGiderler).ThenInclude(e => e.Birim)
                .Include(i => i.IhaleKalemler).ThenInclude(k => k.Birim)
                .FirstOrDefaultAsync(i => i.Id == id && i.Aktif);

        public async Task<int> CreateAsync(Ihale ihale)
        {
            ihale.OlusturmaTarihi = DateTime.Now;
            _db.Ihaleler.Add(ihale);
            await _db.SaveChangesAsync();
            return ihale.Id;
        }

        public async Task UpdateAsync(Ihale ihale)
        {
            ihale.GuncellenmeTarihi = DateTime.Now;
            _db.Ihaleler.Update(ihale);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ihale = await _db.Ihaleler.FindAsync(id);
            if (ihale != null) { ihale.Aktif = false; await _db.SaveChangesAsync(); }
        }

        // ── Özet Hesaplama ────────────────────────────────────────────────────

        public async Task<OzetViewModel> GetOzetAsync(int ihaleId)
        {
            var ihale = await GetByIdAsync(ihaleId);
            if (ihale == null) throw new Exception("İhale bulunamadı.");

            var birimler = await _db.Birimler.Where(b => b.Aktif).ToListAsync();

            var personelToplam = ihale.PersonelGiderler.Sum(p => p.ToplamTRY);
            var ekGiderToplam = ihale.EkGiderler.Sum(e => e.TutarTRY);
            var toplamSUT = ihale.IhaleKalemler.Sum(k => k.ToplamSutFiyat);
            var toplamSutPuani = ihale.IhaleKalemler.Sum(k => k.ToplamSUT);


            var genelGider = (personelToplam + ekGiderToplam) * ihale.GenelGiderOran;
            var genelToplam = personelToplam + ekGiderToplam + genelGider + toplamSUT;


            // Maliyet / SUT oranı
            var sutPuani = toplamSutPuani > 0 ? genelToplam / toplamSutPuani : 0;


            // Birim bazlı özet
            var birimOzetler = birimler.Select(b => new BirimOzetSatir
            {
                BirimKodu = b.BirimKodu,
                BirimAdi = b.BirimAdi,
                PersonelTRY = ihale.PersonelGiderler.Where(p => p.BirimId == b.Id).Sum(p => p.ToplamTRY),
                EkGiderTRY = ihale.EkGiderler.Where(e => e.BirimId == b.Id).Sum(e => e.TutarTRY),
                ToplamSUT = ihale.IhaleKalemler.Where(k => k.BirimId == b.Id).Sum(k => k.ToplamSutFiyat),
                ToplamMaliyet = ihale.PersonelGiderler.Where(p => p.BirimId == b.Id).Sum(p => p.ToplamTRY)
                              + ihale.EkGiderler.Where(e => e.BirimId == b.Id).Sum(e => e.TutarTRY)
                              + ihale.IhaleKalemler.Where(k => k.BirimId == b.Id).Sum(k => k.ToplamSutFiyat)
            }).ToList();

            return new OzetViewModel
            {
                Ihale = ihale,
                PersonelToplam = personelToplam,
                EkGiderToplam = ekGiderToplam,
                GenelGider = genelGider,
                ToplamSutFiyat = toplamSUT,
                GenelToplam = genelToplam,
                ToplamMaliyetSutPuani = sutPuani,
                BirimOzetler = birimOzetler,
                ToplamSutPuani = toplamSutPuani
            };
        }

        // ── Birimler ─────────────────────────────────────────────────────────

        public async Task<List<Birim>> GetBirimlerAsync() =>
            await _db.Birimler.Where(b => b.Aktif).OrderBy(b => b.BirimKodu).ToListAsync();

        public async Task<Birim> SaveBirimAsync(Birim birim)
        {
            if (birim.Id == 0) _db.Birimler.Add(birim);
            else _db.Birimler.Update(birim);
            await _db.SaveChangesAsync();
            return birim;
        }

        // ── Personel ─────────────────────────────────────────────────────────

        public async Task<PersonelGider> SavePersonelAsync(PersonelGider p)
        {
            if (p.Id == 0) _db.PersonelGiderler.Add(p);
            else _db.PersonelGiderler.Update(p);
            await _db.SaveChangesAsync();
            return p;
        }

        public async Task DeletePersonelAsync(int id)
        {
            var p = await _db.PersonelGiderler.FindAsync(id);
            if (p != null) { _db.PersonelGiderler.Remove(p); await _db.SaveChangesAsync(); }
        }

        // ── Ek Gider ─────────────────────────────────────────────────────────

        public async Task<EkGider> SaveEkGiderAsync(EkGider e)
        {
            if (e.Id == 0) _db.EkGiderler.Add(e);
            else _db.EkGiderler.Update(e);
            await _db.SaveChangesAsync();
            return e;
        }

        public async Task DeleteEkGiderAsync(int id)
        {
            var e = await _db.EkGiderler.FindAsync(id);
            if (e != null) { _db.EkGiderler.Remove(e); await _db.SaveChangesAsync(); }
        }

        // ── Kalemler ─────────────────────────────────────────────────────────

        public async Task<IhaleKalem> SaveKalemAsync(IhaleKalem k)
        {
            if (k.Id == 0) _db.IhaleKalemler.Add(k);
            else _db.IhaleKalemler.Update(k);
            await _db.SaveChangesAsync();
            return k;
        }

        public async Task DeleteKalemAsync(int id)
        {
            var k = await _db.IhaleKalemler.FindAsync(id);
            if (k != null) { _db.IhaleKalemler.Remove(k); await _db.SaveChangesAsync(); }
        }
    }
}
