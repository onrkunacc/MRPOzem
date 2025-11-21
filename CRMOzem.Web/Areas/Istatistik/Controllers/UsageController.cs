using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik")]
    public class UsageController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public UsageController(IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
        {
            try
            {
                var usages = await _statisticsService.GetUsagesAsync(hospitalId ?? 0, startDate, endDate, ct);

                var viewModel = usages.Select(u => new UsageViewModel
                {
                    Id = u.Id,
                    HospitalID = u.HospitalID,
                    HospitalName = u.Hospital.HospitalName,
                    TestID = u.TestID,
                    TestName = u.Test.TestName,
                    Month = u.Month,
                    TestCount = u.TestCount
                }).ToList();

                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
                ViewBag.SelectedHospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            try
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName");
                return View(new UsageCreateViewModel { Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1) });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsageCreateViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", model.TestID);
                return View(model);
            }

            try
            {
                await _statisticsService.AddUsageAsync(model.HospitalID, model.TestID, model.Month, model.TestCount, ct);
                TempData["SuccessMessage"] = "Kullanım başarıyla kaydedildi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            try
            {
                var usage = await _context.MonthlyStatistics
                    .AsNoTracking()
                    .Include(u => u.Hospital)
                    .Include(u => u.Test)
                    .FirstOrDefaultAsync(u => u.Id == id, ct);

                if (usage == null)
                {
                    TempData["ErrorMessage"] = "Kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                var model = new UsageEditViewModel
                {
                    Id = usage.Id,
                    HospitalID = usage.HospitalID,
                    TestID = usage.TestID,
                    Month = usage.Month,
                    TestCount = usage.TestCount
                };

                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", model.TestID);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UsageEditViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", model.TestID);
                return View(model);
            }

            try
            {
                var usage = await _context.MonthlyStatistics.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
                if (usage == null)
                {
                    TempData["ErrorMessage"] = "Kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                usage.HospitalID = model.HospitalID;
                usage.TestID = model.TestID;
                usage.Month = new DateTime(model.Month.Year, model.Month.Month, 1);
                usage.TestCount = model.TestCount;

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Kullanım başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", $"Güncelleme hatası: {ex.Message}");
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", model.TestID);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                var usage = await _context.MonthlyStatistics.FindAsync(new object?[] { id }, ct);
                if (usage == null)
                {
                    TempData["Error"] = "Kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                _context.MonthlyStatistics.Remove(usage);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Kayıt başarıyla silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
