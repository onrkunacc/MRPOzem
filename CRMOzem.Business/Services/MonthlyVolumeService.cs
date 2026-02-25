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
    public class MonthlyVolumeService : IMonthlyVolumeService
    {
        private readonly ApplicationDbContext _db;

        public MonthlyVolumeService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<MonthlyVolumeEntryVm> GetEntryAsync(int hospitalId, int year, int month)
        {
            var hospital = await _db.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId);

            if (hospital == null)
                throw new InvalidOperationException("Hospital not found.");

            // Sadece aktif testler
            var activeTests = await _db.TestTemplates.AsNoTracking()
                .Where(x => x.HospitalID == hospitalId && x.IsActive)
                .Select(x => new { x.TestID, x.Test.TestName, x.Test.SutCode })
                .OrderBy(x => x.TestName)
                .ToListAsync();

            var testIds = activeTests.Select(x => x.TestID).ToList();

            // O ay mevcut girilmiş sayılar
            var existing = await _db.HospitalTestMonthlyVolumes.AsNoTracking()
                .Where(x => x.HospitalID == hospitalId && x.Year == year && x.Month == month && testIds.Contains(x.TestID))
                .Select(x => new { x.TestID, x.Count })
                .ToListAsync();

            var existingMap = existing.ToDictionary(x => x.TestID, x => x.Count);

            var rows = activeTests.Select((t, idx) => new MonthlyVolumeRowVm
            {
                Sira = idx + 1,
                TestID = t.TestID,
                TestName = t.TestName,
                SutCode = t.SutCode,
                Count = existingMap.TryGetValue(t.TestID, out var c) ? c : 0
            }).ToList();

            return new MonthlyVolumeEntryVm
            {
                HospitalID = hospitalId,
                HospitalName = hospital.HospitalName,
                Year = year,
                Month = month,
                Rows = rows
            };
        }

        public async Task SaveAsync(int hospitalId, int year, int month, Dictionary<int, int> countsByTestId)
        {
            var testIds = countsByTestId.Keys.ToList();

            var existing = await _db.HospitalTestMonthlyVolumes
                .Where(x => x.HospitalID == hospitalId && x.Year == year && x.Month == month && testIds.Contains(x.TestID))
                .ToListAsync();

            var map = existing.ToDictionary(x => x.TestID, x => x);

            foreach (var kv in countsByTestId)
            {
                var testId = kv.Key;
                var count = kv.Value < 0 ? 0 : kv.Value;

                if (map.TryGetValue(testId, out var row))
                {
                    row.Count = count;
                }
                else
                {
                    _db.HospitalTestMonthlyVolumes.Add(new HospitalTestMonthlyVolume
                    {
                        HospitalID = hospitalId,
                        TestID = testId,
                        Year = year,
                        Month = month,
                        Count = count
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task CopyPreviousMonthAsync(int hospitalId, int year, int month, bool overwriteExisting)
        {
            var (py, pm) = month == 1 ? (year - 1, 12) : (year, month - 1);

            var prev = await _db.HospitalTestMonthlyVolumes.AsNoTracking()
                .Where(x => x.HospitalID == hospitalId && x.Year == py && x.Month == pm)
                .Select(x => new { x.TestID, x.Count })
                .ToListAsync();

            if (!prev.Any()) return;

            var prevMap = prev.ToDictionary(x => x.TestID, x => x.Count);

            var current = await _db.HospitalTestMonthlyVolumes
                .Where(x => x.HospitalID == hospitalId && x.Year == year && x.Month == month)
                .ToListAsync();

            var currentMap = current.ToDictionary(x => x.TestID, x => x);

            foreach (var kv in prevMap)
            {
                if (currentMap.TryGetValue(kv.Key, out var row))
                {
                    if (overwriteExisting)
                        row.Count = kv.Value;
                }
                else
                {
                    _db.HospitalTestMonthlyVolumes.Add(new HospitalTestMonthlyVolume
                    {
                        HospitalID = hospitalId,
                        TestID = kv.Key,
                        Year = year,
                        Month = month,
                        Count = kv.Value
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
