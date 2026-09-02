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
    public class HospitalTestSetService : IHospitalTestSetService
    {
        private readonly ApplicationDbContext _db;

        public HospitalTestSetService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<HospitalTestSetVm> GetAsync(int hospitalId)
        {
            var hospital = await _db.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId);

            if (hospital == null)
                throw new InvalidOperationException("Hospital not found.");

            var allTests = await _db.Tests.AsNoTracking()
                .Select(t => new { t.TestID, t.TestName, t.SutCode, t.GirisTipi, t.KutuBasinaAdet, t.Kategori, t.Platform })
                .OrderBy(t => t.TestName)
                .ToListAsync();

            var templates = await _db.TestTemplates.AsNoTracking()
                .Where(x => x.HospitalID == hospitalId)
                .Select(x => new { x.TestID, x.IsActive })
                .ToListAsync();

            var templateMap = templates.ToDictionary(x => x.TestID, x => x.IsActive);

            var rows = allTests.Select(t => new HospitalTestSetRowVm
            {
                TestID = t.TestID,
                TestName = t.TestName,
                SutCode = t.SutCode,
                Kategori = t.Kategori,        // ← YENİ
                Platform = t.Platform,
                GirisTipi = t.GirisTipi,
                KutuBasinaAdet = t.KutuBasinaAdet,
                IsActive = templateMap.TryGetValue(t.TestID, out var active) ? active : false,
                IsLinked = templateMap.ContainsKey(t.TestID)
            }).ToList();

            return new HospitalTestSetVm
            {
                HospitalID = hospitalId,
                HospitalName = hospital.HospitalName,
                Rows = rows
            };
        }

        // Standart set: sistemdeki tüm testleri hastaneye bağla (eksikleri ekle)
        public async Task ApplyStandardSetAsync(int hospitalId)
        {
            var allTestIds = await _db.Tests.Select(t => t.TestID).ToListAsync();

            var existingTestIds = await _db.TestTemplates
                .Where(x => x.HospitalID == hospitalId)
                .Select(x => x.TestID)
                .ToListAsync();

            var missing = allTestIds.Except(existingTestIds).ToList();

            foreach (var testId in missing)
            {
                _db.TestTemplates.Add(new TestTemplate
                {
                    HospitalID = hospitalId,
                    TestID = testId,
                    IsActive = true
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task SetAllActiveAsync(int hospitalId, bool isActive)
        {
            var templates = await _db.TestTemplates
                .Where(x => x.HospitalID == hospitalId)
                .ToListAsync();

            foreach (var t in templates)
                t.IsActive = isActive;

            await _db.SaveChangesAsync();
        }

        // Checkbox’larla toplu güncelleme:
        // - Linked değilse ve aktif seçildiyse: yeni TestTemplate ekle
        // - Linked ise: IsActive güncelle
        public async Task UpdateActiveFlagsAsync(int hospitalId, Dictionary<int, bool> activeByTestId)
        {
            var testIds = activeByTestId.Keys.ToList();

            var existing = await _db.TestTemplates
                .Where(x => x.HospitalID == hospitalId && testIds.Contains(x.TestID))
                .ToListAsync();

            var existingMap = existing.ToDictionary(x => x.TestID, x => x);

            foreach (var kv in activeByTestId)
            {
                var testId = kv.Key;
                var isActive = kv.Value;

                if (existingMap.TryGetValue(testId, out var template))
                {
                    template.IsActive = isActive;
                }
                else
                {
                    // kullanıcı aktif yaptıysa bağla
                    if (isActive)
                    {
                        _db.TestTemplates.Add(new TestTemplate
                        {
                            HospitalID = hospitalId,
                            TestID = testId,
                            IsActive = true
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
