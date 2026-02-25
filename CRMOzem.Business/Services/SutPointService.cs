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
    public class SutPointService : ISutPointService
    {
        private readonly ApplicationDbContext _db;

        public SutPointService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<SutPointVm> GetAsync(int year)
        {
            var tests = await _db.Tests.AsNoTracking()
                .Select(t => new { t.TestID, t.TestName, t.SutCode })
                .OrderBy(t => t.TestName)
                .ToListAsync();

            var points = await _db.TestSutPoints.AsNoTracking()
                .Where(p => p.Year == year)
                .Select(p => new { p.TestID, p.Point })
                .ToListAsync();

            var map = points.ToDictionary(x => x.TestID, x => x.Point);

            return new SutPointVm
            {
                Year = year,
                Rows = tests.Select(t => new SutPointRowVm
                {
                    TestID = t.TestID,
                    TestName = t.TestName,
                    SutCode = t.SutCode,
                    Point = map.TryGetValue(t.TestID, out var pt) ? pt : (decimal?)null
                }).ToList()
            };
        }

        // Upsert: aynı (TestID, Year) varsa update, yoksa insert
        public async Task SaveAsync(int year, Dictionary<int, decimal?> pointsByTestId)
        {
            var testIds = pointsByTestId.Keys.ToList();

            var existing = await _db.TestSutPoints
                .Where(x => x.Year == year && testIds.Contains(x.TestID))
                .ToListAsync();

            var map = existing.ToDictionary(x => x.TestID, x => x);

            foreach (var kv in pointsByTestId)
            {
                var testId = kv.Key;
                var point = kv.Value;

                // boş bırakıldıysa kaydetmeyelim (isterseniz 0 yazdırabiliriz)
                if (!point.HasValue)
                    continue;

                if (map.TryGetValue(testId, out var row))
                {
                    row.Point = point.Value;
                }
                else
                {
                    _db.TestSutPoints.Add(new TestSutPoint
                    {
                        TestID = testId,
                        Year = year,
                        Point = point.Value
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        // Bir önceki yıldan yeni yıla kopyala
        public async Task CopyFromYearAsync(int fromYear, int toYear, bool overwrite)
        {
            var from = await _db.TestSutPoints.AsNoTracking()
                .Where(x => x.Year == fromYear)
                .Select(x => new { x.TestID, x.Point })
                .ToListAsync();

            if (!from.Any()) return;

            var toExisting = await _db.TestSutPoints
                .Where(x => x.Year == toYear)
                .ToListAsync();

            var toMap = toExisting.ToDictionary(x => x.TestID, x => x);

            foreach (var f in from)
            {
                if (toMap.TryGetValue(f.TestID, out var row))
                {
                    if (overwrite)
                        row.Point = f.Point;
                }
                else
                {
                    _db.TestSutPoints.Add(new TestSutPoint
                    {
                        TestID = f.TestID,
                        Year = toYear,
                        Point = f.Point
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
