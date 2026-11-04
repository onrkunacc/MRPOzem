using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class ResmiTatilService : IResmiTatilService
    {
        private readonly ApplicationDbContext _context;

        public ResmiTatilService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tarihi her yıl değişmeyen milli bayram ve genel tatiller.
        /// Dini bayramlar (Ramazan / Kurban) hicri takvime göre kaydığı ve
        /// Türkiye'de resmi tatil süresi Cumhurbaşkanı kararıyla uzatılabildiği için
        /// bu listeye dahil edilmez — İK her yıl elle girer.
        /// </summary>
        private static readonly (int Ay, int Gun, string Ad, bool TamGun)[] SabitTatiller =
        {
            (1,  1,  "Yılbaşı",                                      true),
            (4,  23, "Ulusal Egemenlik ve Çocuk Bayramı",            true),
            (5,  1,  "Emek ve Dayanışma Günü",                       true),
            (5,  19, "Atatürk'ü Anma, Gençlik ve Spor Bayramı",      true),
            (7,  15, "Demokrasi ve Millî Birlik Günü",               true),
            (8,  30, "Zafer Bayramı",                                true),
            (10, 28, "Cumhuriyet Bayramı Arifesi",                   false), // yarım gün
            (10, 29, "Cumhuriyet Bayramı",                           true),
        };

        public async Task<List<ResmiTatil>> GetByYearAsync(int yil)
        {
            return await _context.Set<ResmiTatil>()
                .AsNoTracking()
                .Where(t => t.Yil == yil)
                .OrderBy(t => t.Tarih)
                .ToListAsync();
        }

        public async Task<ResmiTatil?> GetByIdAsync(int id)
        {
            return await _context.Set<ResmiTatil>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<DateTime>> GetTamGunTatillerAsync(DateTime baslangic, DateTime bitis)
        {
            var bas = baslangic.Date;
            var bit = bitis.Date;

            return await _context.Set<ResmiTatil>()
                .AsNoTracking()
                .Where(t => t.TamGun && t.Tarih >= bas && t.Tarih <= bit)
                .Select(t => t.Tarih)
                .ToListAsync();
        }

        public async Task<(int Eklenen, int Atlanan)> SabitTatilleriOlusturAsync(int yil)
        {
            if (yil < 2000 || yil > 2100)
                return (0, 0);

            var mevcut = await _context.Set<ResmiTatil>()
                .Where(t => t.Yil == yil)
                .Select(t => t.Tarih)
                .ToListAsync();

            var mevcutSet = mevcut.Select(t => t.Date).ToHashSet();

            int eklenen = 0, atlanan = 0;

            foreach (var (ay, gun, ad, tamGun) in SabitTatiller)
            {
                var tarih = new DateTime(yil, ay, gun);

                if (mevcutSet.Contains(tarih))
                {
                    atlanan++;
                    continue;
                }

                _context.Set<ResmiTatil>().Add(new ResmiTatil
                {
                    Tarih = tarih,
                    Ad = ad,
                    TamGun = tamGun,
                    Yil = yil,
                    Aciklama = "Otomatik oluşturuldu (sabit tarihli milli bayram)",
                    OlusturmaTarihi = DateTime.Now
                });

                mevcutSet.Add(tarih);
                eklenen++;
            }

            if (eklenen > 0)
                await _context.SaveChangesAsync();

            return (eklenen, atlanan);
        }

        public async Task<(bool Basarili, string? Hata)> EkleAsync(ResmiTatil tatil)
        {
            if (tatil == null)
                return (false, "Kayıt boş olamaz.");

            if (string.IsNullOrWhiteSpace(tatil.Ad))
                return (false, "Tatil adı zorunludur.");

            tatil.Tarih = tatil.Tarih.Date;
            tatil.Yil = tatil.Tarih.Year;
            tatil.Ad = tatil.Ad.Trim();
            tatil.OlusturmaTarihi = DateTime.Now;

            var varMi = await _context.Set<ResmiTatil>()
                .AnyAsync(t => t.Tarih == tatil.Tarih);

            if (varMi)
                return (false, $"{tatil.Tarih:dd.MM.yyyy} tarihi zaten kayıtlı.");

            _context.Set<ResmiTatil>().Add(tatil);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(int Eklenen, int Atlanan, string? Hata)> AralikEkleAsync(
            DateTime baslangic, DateTime bitis, string ad, bool tamGun, string? aciklama)
        {
            if (string.IsNullOrWhiteSpace(ad))
                return (0, 0, "Tatil adı zorunludur.");

            var bas = baslangic.Date;
            var bit = bitis.Date;

            if (bit < bas)
                return (0, 0, "Bitiş tarihi başlangıçtan önce olamaz.");

            if ((bit - bas).TotalDays > 30)
                return (0, 0, "Aralık en fazla 30 gün olabilir.");

            var mevcut = await _context.Set<ResmiTatil>()
                .Where(t => t.Tarih >= bas && t.Tarih <= bit)
                .Select(t => t.Tarih)
                .ToListAsync();

            var mevcutSet = mevcut.Select(t => t.Date).ToHashSet();

            int eklenen = 0, atlanan = 0;
            int toplamGun = (int)(bit - bas).TotalDays + 1;
            int sira = 1;

            for (var d = bas; d <= bit; d = d.AddDays(1), sira++)
            {
                if (mevcutSet.Contains(d))
                {
                    atlanan++;
                    continue;
                }

                // Çok günlü bayramlarda gün numarası ekle: "Kurban Bayramı 2. Gün"
                var gunAdi = toplamGun > 1 ? $"{ad.Trim()} {sira}. Gün" : ad.Trim();

                _context.Set<ResmiTatil>().Add(new ResmiTatil
                {
                    Tarih = d,
                    Ad = gunAdi,
                    TamGun = tamGun,
                    Yil = d.Year,
                    Aciklama = aciklama?.Trim(),
                    OlusturmaTarihi = DateTime.Now
                });

                eklenen++;
            }

            if (eklenen > 0)
                await _context.SaveChangesAsync();

            return (eklenen, atlanan, null);
        }

        public async Task<(bool Basarili, string? Hata)> GuncelleAsync(ResmiTatil tatil)
        {
            var kayit = await _context.Set<ResmiTatil>()
                .FirstOrDefaultAsync(t => t.Id == tatil.Id);

            if (kayit == null)
                return (false, "Kayıt bulunamadı.");

            if (string.IsNullOrWhiteSpace(tatil.Ad))
                return (false, "Tatil adı zorunludur.");

            var yeniTarih = tatil.Tarih.Date;

            if (yeniTarih != kayit.Tarih.Date)
            {
                var cakisma = await _context.Set<ResmiTatil>()
                    .AnyAsync(t => t.Tarih == yeniTarih && t.Id != tatil.Id);

                if (cakisma)
                    return (false, $"{yeniTarih:dd.MM.yyyy} tarihi zaten kayıtlı.");
            }

            kayit.Tarih = yeniTarih;
            kayit.Yil = yeniTarih.Year;
            kayit.Ad = tatil.Ad.Trim();
            kayit.TamGun = tatil.TamGun;
            kayit.Aciklama = tatil.Aciklama?.Trim();

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Basarili, string? Hata)> SilAsync(int id)
        {
            var kayit = await _context.Set<ResmiTatil>()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (kayit == null)
                return (false, "Kayıt bulunamadı.");

            _context.Set<ResmiTatil>().Remove(kayit);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<List<int>> GetAvailableYearsAsync()
        {
            var yillar = await _context.Set<ResmiTatil>()
                .AsNoTracking()
                .Select(t => t.Yil)
                .Distinct()
                .ToListAsync();

            var simdi = DateTime.Now.Year;
            if (!yillar.Contains(simdi)) yillar.Add(simdi);
            if (!yillar.Contains(simdi + 1)) yillar.Add(simdi + 1);

            return yillar.OrderByDescending(y => y).ToList();
        }
    }
}
