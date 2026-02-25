using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Istatistik;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class SutComparisonService : ISutComparisonService
    {
        private readonly ApplicationDbContext _db;

        public SutComparisonService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<SutComparisonVm> BuildMonthlyAsync(int hospitalId, int year, int month, int ihaleYear, int currentYear)
        {
            var hospital = await _db.Hospitals
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId);

            if (hospital == null)
                throw new InvalidOperationException("Hospital not found.");

            // O ay için hastanenin test adetleri (tetkik sayıları)
            var volumes = await _db.HospitalTestMonthlyVolumes
                .AsNoTracking()
                .Where(v => v.HospitalID == hospitalId && v.Year == year && v.Month == month)
                .Select(v => new { v.TestID, v.Count })
                .ToListAsync();

            var testIds = volumes.Select(v => v.TestID).Distinct().ToList();

            // Test bilgileri
            var tests = await _db.Tests
                .AsNoTracking()
                .Where(t => testIds.Contains(t.TestID))
                .Select(t => new { t.TestID, t.TestName, t.SutCode })
                .ToListAsync();

            // İhale ve güncel yıl puanları
            var points = await _db.TestSutPoints
                .AsNoTracking()
                .Where(p => testIds.Contains(p.TestID) && (p.Year == ihaleYear || p.Year == currentYear))
                .Select(p => new { p.TestID, p.Year, p.Point })
                .ToListAsync();

            decimal GetPoint(int testId, int pYear)
                => points.FirstOrDefault(x => x.TestID == testId && x.Year == pYear)?.Point ?? 0m;

            var rows = new List<SutComparisonRowVm>();
            int sira = 1;

            foreach (var t in tests.OrderBy(x => x.TestName))
            {
                var count = volumes.FirstOrDefault(v => v.TestID == t.TestID)?.Count ?? 0;

                rows.Add(new SutComparisonRowVm
                {
                    Sira = sira++,
                    MalzemeninAdi = t.TestName,
                    SutKodu = t.SutCode,
                    TetkikSayisi = count,
                    SutPuaniIhale = GetPoint(t.TestID, ihaleYear),
                    GuncelSut = GetPoint(t.TestID, currentYear)
                });
            }

            return new SutComparisonVm
            {
                HospitalID = hospital.HospitalID,
                HospitalName = hospital.HospitalName,
                Year = year,
                Month = month,
                IhaleYear = ihaleYear,
                CurrentYear = currentYear,
                Rows = rows
            };
        }
    }
}
