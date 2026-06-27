using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class UsageTemplateService : IUsageTemplateService
    {
        private readonly ApplicationDbContext _db;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public UsageTemplateService(ApplicationDbContext db)
        {
            _db = db;
        }

        private static DateTime NormalizeMonth(DateTime month)
            => new DateTime(month.Year, month.Month, 1);

        public async Task<UsageTemplateFormVm> BuildFormAsync(int hospitalId, DateTime month, CancellationToken ct = default)
        {
            try
            {
                var normalizedMonth = NormalizeMonth(month);

                var hospital = await _db.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == hospitalId, ct)
                    ?? throw new InvalidOperationException("Hastane bulunamadı.");

                // Şablon: aktif testler
                var aktifTestler = await _db.TestTemplates.AsNoTracking()
                    .Include(t => t.Test)
                    .Where(t => t.HospitalID == hospitalId && t.IsActive)
                    .Select(t => new
                    {
                        t.TestID,
                        t.Test.TestName,
                        t.Test.SutCode,
                        t.Test.GirisTipi
                    })
                    .OrderBy(t => t.TestName)
                    .ToListAsync(ct);

                // Mevcut tüketim kayıtları
                var mevcutKayitlar = await _db.MonthlyStatistics.AsNoTracking()
                    .Where(s => s.HospitalID == hospitalId && s.Month == normalizedMonth)
                    .ToDictionaryAsync(s => s.TestID, s => s.TestCount, ct);

                var vm = new UsageTemplateFormVm
                {
                    HospitalID = hospitalId,
                    HospitalName = hospital.HospitalName,
                    Month = normalizedMonth,
                    MonthLabel = normalizedMonth.ToString("MMMM yyyy", TR),
                    Rows = aktifTestler.Select(t => new UsageTemplateRowVm
                    {
                        TestID = t.TestID,
                        TestName = t.TestName,
                        SutCode = t.SutCode,
                        GirisTipi = t.GirisTipi,
                        MevcutAdet = mevcutKayitlar.GetValueOrDefault(t.TestID, 0)
                    }).ToList()
                };

                return vm;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException("İstatistik formu yüklenemedi: " + ex.Message, ex);
            }
        }

        public async Task<UsageSaveResult> SaveAsync(UsageTemplateSaveVm model, CancellationToken ct = default)
        {
            var result = new UsageSaveResult { Success = false };

            try
            {
                if (model == null)
                {
                    result.ErrorMessage = "Veri alınamadı.";
                    return result;
                }

                if (model.HospitalID <= 0)
                {
                    result.ErrorMessage = "Hastane seçilmedi.";
                    return result;
                }

                var normalizedMonth = NormalizeMonth(model.Month);
                var mode = (model.ConflictMode ?? "add").ToLowerInvariant();

                var validRows = (model.Rows ?? new())
                    .Where(r => r.TestID > 0 && r.Amount.HasValue && r.Amount.Value > 0)
                    .ToList();

                if (validRows.Count == 0)
                {
                    result.ErrorMessage = "Kaydedilecek geçerli satır yok.";
                    return result;
                }

                var testIds = validRows.Select(r => r.TestID).ToList();
                var mevcutlar = await _db.MonthlyStatistics
                    .Where(s => s.HospitalID == model.HospitalID
                                && s.Month == normalizedMonth
                                && testIds.Contains(s.TestID))
                    .ToListAsync(ct);

                var mevcutMap = mevcutlar.ToDictionary(s => s.TestID, s => s);

                foreach (var row in validRows)
                {
                    var amount = row.Amount!.Value;

                    if (mevcutMap.TryGetValue(row.TestID, out var existing))
                    {
                        switch (mode)
                        {
                            case "skip":
                                result.AtlananSatir++;
                                continue;

                            case "overwrite":
                                existing.TestCount = amount;
                                result.GuncellenenSatir++;
                                result.ToplamAdet += amount;
                                break;

                            case "add":
                            default:
                                existing.TestCount += amount;
                                result.GuncellenenSatir++;
                                result.ToplamAdet += amount;
                                break;
                        }
                    }
                    else
                    {
                        _db.MonthlyStatistics.Add(new MonthlyStatistics
                        {
                            HospitalID = model.HospitalID,
                            TestID = row.TestID,
                            Month = normalizedMonth,
                            TestCount = amount
                        });
                        result.EklenenSatir++;
                        result.ToplamAdet += amount;
                    }
                }

                await _db.SaveChangesAsync(ct);
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Kayıt sırasında hata: " + ex.Message;
                return result;
            }
        }
    }
}