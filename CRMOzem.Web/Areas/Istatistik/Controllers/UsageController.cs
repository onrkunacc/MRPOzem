using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
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
        private readonly IUsageTemplateService _usageTemplateService;
        private readonly ApplicationDbContext _context;

        public UsageController(
            IStatisticsService statisticsService,
            IUsageTemplateService usageTemplateService,
            ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _usageTemplateService = usageTemplateService;
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

                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName");
                ViewBag.SelectedHospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return View(new List<UsageViewModel>());
            }
        }

        // ═════════════════════════════════════════════
        // YENİ — Şablona bağlı toplu istatistik girişi
        // ═════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Create(int? hospitalId, DateTime? month, CancellationToken ct)
        {
            try
            {
                await LoadHospitalsAsync(hospitalId, ct);

                if (hospitalId == null || hospitalId <= 0)
                {
                    ViewBag.SelectedMonth = (month ?? DateTime.Now).ToString("yyyy-MM");
                    return View(model: null);
                }

                var m = month ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var vm = await _usageTemplateService.BuildFormAsync(hospitalId.Value, m, ct);

                ViewBag.SelectedMonth = m.ToString("yyyy-MM");
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Form yüklenemedi: " + ex.Message;
                await LoadHospitalsAsync(hospitalId, ct);
                return View(model: null);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsageTemplateSaveVm model, CancellationToken ct)
        {
            try
            {
                if (model == null || model.HospitalID <= 0)
                {
                    TempData["Error"] = "Geçersiz veri.";
                    return RedirectToAction(nameof(Create));
                }

                var result = await _usageTemplateService.SaveAsync(model, ct);

                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage ?? "Kayıt başarısız.";
                    return RedirectToAction(nameof(Create), new
                    {
                        hospitalId = model.HospitalID,
                        month = model.Month.ToString("yyyy-MM-dd")
                    });
                }

                var mesaj = $"İşlem başarılı: " +
                    $"{result.EklenenSatir} yeni, {result.GuncellenenSatir} güncellendi" +
                    (result.AtlananSatir > 0 ? $", {result.AtlananSatir} atlandı" : "") +
                    $". Toplam {result.ToplamAdet} test.";

                TempData["SuccessMessage"] = mesaj;
                return RedirectToAction(nameof(Index), new { hospitalId = model.HospitalID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Create), new
                {
                    hospitalId = model?.HospitalID,
                    month = model?.Month.ToString("yyyy-MM-dd")
                });
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
                    TempData["Error"] = "Kayıt bulunamadı.";
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

                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(
                    await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct),
                    "TestID", "TestName", model.TestID);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UsageEditViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(
                    await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct),
                    "TestID", "TestName", model.TestID);
                return View(model);
            }

            try
            {
                var usage = await _context.MonthlyStatistics.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
                if (usage == null)
                {
                    TempData["Error"] = "Kayıt bulunamadı.";
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
                ModelState.AddModelError("", "Güncelleme hatası: " + ex.Message);
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(
                    await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct),
                    "TestID", "TestName", model.TestID);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
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
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task LoadHospitalsAsync(int? selectedHospitalId, CancellationToken ct)
        {
            ViewBag.Hospitals = await _context.Hospitals.AsNoTracking()
                .OrderBy(x => x.HospitalName)
                .Select(x => new SelectListItem
                {
                    Value = x.HospitalID.ToString(),
                    Text = x.HospitalName,
                    Selected = selectedHospitalId.HasValue && x.HospitalID == selectedHospitalId.Value
                })
                .ToListAsync(ct);
        }
    }
}