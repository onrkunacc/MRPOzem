using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Istatistik;
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
    public class IstatistikService : IIstatistikService
    {
        private readonly ApplicationDbContext _db;

        public IstatistikService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IstatistikDashboardVm> GetDashboardAsync(int yil, int[] aylar)
        {
            // UI boş gönderirse varsayılan: tüm aylar
            var selectedMonths = (aylar != null && aylar.Length > 0)
                ? aylar.Distinct().Where(x => x >= 1 && x <= 12).OrderBy(x => x).ToArray()
                : Enumerable.Range(1, 12).ToArray();

            var kurumlar = await _db.Kurumlar
                .AsNoTracking()
                .Where(x => x.AktifMi)
                .OrderBy(x => x.Ad)
                .Select(x => new
                {
                    x.Id,
                    x.Ad,
                    x.IhaleTarihi,
                    x.IhaleNumarasi
                })
                .ToListAsync();

            // Seçili aylar için kayıtları tek sorguda çek
            var kurumIds = kurumlar.Select(k => k.Id).ToArray();

            var aylikKayitlar = await _db.KurumIstatistikAyliklar
                .AsNoTracking()
                .Where(x => x.Yil == yil
                            && selectedMonths.Contains(x.Ay)
                            && kurumIds.Contains(x.KurumId))
                .Select(x => new
                {
                    x.KurumId,
                    x.Ay,
                    x.GeldiMi,
                    x.Not,
                    x.UpdatedAt
                })
                .ToListAsync();

            var vm = new IstatistikDashboardVm
            {
                Yil = yil,
                SeciliAylar = selectedMonths.ToList(),
                Kurumler = kurumlar.Select(k => new KurumRowVm
                {
                    KurumId = k.Id,
                    KurumAdi = k.Ad,
                    IhaleTarihi = k.IhaleTarihi,
                    IhaleNumarasi = k.IhaleNumarasi,
                    Aylar = selectedMonths.Select(ay =>
                    {
                        var rec = aylikKayitlar.FirstOrDefault(r => r.KurumId == k.Id && r.Ay == ay);
                        return new AyDurumVm
                        {
                            Ay = ay,
                            GeldiMi = rec?.GeldiMi ?? false,
                            Not = rec?.Not,
                            UpdatedAtText = rec == null ? null : rec.UpdatedAt.ToLocalTime().ToString("g")
                        };
                    }).ToList()
                }).ToList()
            };

            return vm;
        }

        public async Task SetGeldiMiAsync(SetGeldiMiRequest req)
        {
            ValidateMonth(req.Ay);

            var now = DateTime.UtcNow;

            var entity = await _db.KurumIstatistikAyliklar
                .FirstOrDefaultAsync(x => x.KurumId == req.KurumId && x.Yil == req.Yil && x.Ay == req.Ay);

            if (entity == null)
            {
                entity = new KurumIstatistikAylik
                {
                    KurumId = req.KurumId,
                    Yil = req.Yil,
                    Ay = req.Ay,
                    GeldiMi = req.GeldiMi,
                    UpdatedAt = now
                };
                _db.KurumIstatistikAyliklar.Add(entity);
            }
            else
            {
                entity.GeldiMi = req.GeldiMi;
                entity.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();
        }

        public async Task SetNotAsync(SetNotRequest req)
        {
            ValidateMonth(req.Ay);

            var now = DateTime.UtcNow;
            var note = string.IsNullOrWhiteSpace(req.Not) ? null : req.Not.Trim();

            var entity = await _db.KurumIstatistikAyliklar
                .FirstOrDefaultAsync(x => x.KurumId == req.KurumId && x.Yil == req.Yil && x.Ay == req.Ay);

            if (entity == null)
            {
                entity = new KurumIstatistikAylik
                {
                    KurumId = req.KurumId,
                    Yil = req.Yil,
                    Ay = req.Ay,
                    GeldiMi = false,
                    Not = note,
                    UpdatedAt = now
                };
                _db.KurumIstatistikAyliklar.Add(entity);
            }
            else
            {
                entity.Not = note;
                entity.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();
        }

        private static void ValidateMonth(int ay)
        {
            if (ay < 1 || ay > 12)
                throw new ArgumentOutOfRangeException(nameof(ay), "Ay 1-12 arasında olmalı.");
        }
    }
}
