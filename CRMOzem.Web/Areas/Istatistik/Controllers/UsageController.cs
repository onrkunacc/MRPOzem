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

        // Listeleme
        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var usages = await _statisticsService.GetUsagesAsync(hospitalId ?? 0, startDate, endDate);

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

                ViewBag.Hospitals = new SelectList(await _context.Hospitals.ToListAsync(), "HospitalID", "HospitalName");
                ViewBag.SelectedHospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Yeni Kullanım Formu
        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
                ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName");
                return View(new UsageCreateViewModel());
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Yeni Kullanım Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsageCreateViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                    ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName", model.TestID);
                    return View(model);
                }

                await _statisticsService.AddUsageAsync(model.HospitalID, model.TestID, model.Month, model.TestCount);

                TempData["SuccessMessage"] = "Kullanım başarıyla kaydedildi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Düzenleme Formu
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var usage = await _context.MonthlyStatistics
                    .Include(u => u.Hospital)
                    .Include(u => u.Test)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (usage == null)
                {
                    TempData["Error"] = "Kayıt bulunamadı.";
                    return RedirectToAction("Index");
                }

                var model = new UsageEditViewModel
                {
                    Id = usage.Id,
                    HospitalID = usage.HospitalID,
                    TestID = usage.TestID,
                    Month = usage.Month,
                    TestCount = usage.TestCount
                };

                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName", model.TestID);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Düzenleme Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UsageEditViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                    ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName", model.TestID);
                    return View(model);
                }

                var usage = await _context.MonthlyStatistics.FindAsync(model.Id);
                if (usage == null)
                {
                    TempData["Error"] = "Kayıt bulunamadı.";
                    return RedirectToAction("Index");
                }

                usage.HospitalID = model.HospitalID;
                usage.TestID = model.TestID;
                usage.Month = model.Month;
                usage.TestCount = model.TestCount;

                _context.MonthlyStatistics.Update(usage);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Kullanım başarıyla güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var usage = await _context.MonthlyStatistics.FindAsync(id);
                if (usage == null)
                {
                    TempData["Error"] = "Kayıt bulunamadı.";
                    return RedirectToAction("Index");
                }

                _context.MonthlyStatistics.Remove(usage);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Kayıt başarıyla silindi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
