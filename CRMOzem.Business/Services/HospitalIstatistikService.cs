using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Istatistik;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Business.Services
{
    public class HospitalIstatistikService : IHospitalIstatistikService
    {
        private readonly ApplicationDbContext _db;

        public HospitalIstatistikService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<HospitalIstatistikDashboardVm> GetDashboardAsync(int yil, int[] aylar)
        {
            try
            {
                // UI boş gönderirse varsayılan: tüm aylar
                var selectedMonths = (aylar != null && aylar.Length > 0)
                    ? aylar.Distinct().Where(x => x >= 1 && x <= 12).OrderBy(x => x).ToArray()
                    : Enumerable.Range(1, 12).ToArray();

                // Sadece aktif hastaneler
                var hospitaller = await _db.Hospitals
                    .AsNoTracking()
                    .Where(x => x.Aktif)
                    .OrderBy(x => x.HospitalName)
                    .Select(x => new
                    {
                        x.HospitalID,
                        x.HospitalName,
                        x.IhaleTarihi,
                        x.IhaleNumarasi
                    })
                    .ToListAsync();

                var hospitalIds = hospitaller.Select(h => h.HospitalID).ToArray();

                // Seçili aylar için tek sorguda istatistikleri çek
                var aylikKayitlar = await _db.HospitalAylikIstatistikler
                    .AsNoTracking()
                    .Where(x => x.Yil == yil
                                && selectedMonths.Contains(x.Ay)
                                && hospitalIds.Contains(x.HospitalID))
                    .Select(x => new
                    {
                        x.HospitalID,
                        x.Ay,
                        x.GeldiMi,
                        x.Not,
                        x.UpdatedAt
                    })
                    .ToListAsync();

                var vm = new HospitalIstatistikDashboardVm
                {
                    Yil = yil,
                    SeciliAylar = selectedMonths.ToList(),
                    Hospitaller = hospitaller.Select(h => new HospitalIstatistikRowVm
                    {
                        HospitalID = h.HospitalID,
                        HospitalAdi = h.HospitalName,
                        IhaleTarihi = h.IhaleTarihi,
                        IhaleNumarasi = h.IhaleNumarasi,
                        Aylar = selectedMonths.Select(ay =>
                        {
                            var rec = aylikKayitlar.FirstOrDefault(r => r.HospitalID == h.HospitalID && r.Ay == ay);
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
            catch (Exception ex)
            {
                throw new InvalidOperationException("Hastane istatistik panosu yüklenemedi: " + ex.Message, ex);
            }
        }

        public async Task SetGeldiMiAsync(SetHospitalGeldiMiRequest req)
        {
            try
            {
                ValidateMonth(req.Ay);

                var now = DateTime.UtcNow;

                var entity = await _db.HospitalAylikIstatistikler
                    .FirstOrDefaultAsync(x => x.HospitalID == req.HospitalID
                                              && x.Yil == req.Yil
                                              && x.Ay == req.Ay);

                if (entity == null)
                {
                    entity = new HospitalAylikIstatistik
                    {
                        HospitalID = req.HospitalID,
                        Yil = req.Yil,
                        Ay = req.Ay,
                        GeldiMi = req.GeldiMi,
                        UpdatedAt = now
                    };
                    _db.HospitalAylikIstatistikler.Add(entity);
                }
                else
                {
                    entity.GeldiMi = req.GeldiMi;
                    entity.UpdatedAt = now;
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Durum güncellenemedi: " + ex.Message, ex);
            }
        }

        public async Task SetNotAsync(SetHospitalNotRequest req)
        {
            try
            {
                ValidateMonth(req.Ay);

                var now = DateTime.UtcNow;
                var note = string.IsNullOrWhiteSpace(req.Not) ? null : req.Not.Trim();

                var entity = await _db.HospitalAylikIstatistikler
                    .FirstOrDefaultAsync(x => x.HospitalID == req.HospitalID
                                              && x.Yil == req.Yil
                                              && x.Ay == req.Ay);

                if (entity == null)
                {
                    entity = new HospitalAylikIstatistik
                    {
                        HospitalID = req.HospitalID,
                        Yil = req.Yil,
                        Ay = req.Ay,
                        GeldiMi = false,
                        Not = note,
                        UpdatedAt = now
                    };
                    _db.HospitalAylikIstatistikler.Add(entity);
                }
                else
                {
                    entity.Not = note;
                    entity.UpdatedAt = now;
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Not güncellenemedi: " + ex.Message, ex);
            }
        }

        private static void ValidateMonth(int ay)
        {
            if (ay < 1 || ay > 12)
                throw new ArgumentOutOfRangeException(nameof(ay), "Ay 1-12 arasında olmalı.");
        }
    }
}