using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik")]
    public class ReportController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public ReportController(IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Hospitals = new SelectList(
                await _context.Hospitals
                    .AsNoTracking()
                    .OrderBy(x => x.HospitalName)
                    .ToListAsync(ct),
                "HospitalID",
                "HospitalName"
            );

            return View(new ReportFilterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ReportFilterViewModel model, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals
                        .AsNoTracking()
                        .OrderBy(x => x.HospitalName)
                        .ToListAsync(ct),
                    "HospitalID",
                    "HospitalName",
                    model.HospitalID
                );

                return View(model);
            }

            // PRG pattern: POST sonrası GET /Result?a yönlendir
            return RedirectToAction("Result", new
            {
                hospitalId = model.HospitalID,
                startDate = model.StartDate?.ToString("yyyy-MM-dd"),
                endDate = model.EndDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public async Task<IActionResult> Result(
            int hospitalId,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            CancellationToken ct = default)
        {
            try
            {
                if (hospitalId <= 0)
                {
                    // Hastane seçilmemişse boş liste göster
                    var empty = new List<HospitalTestBalanceViewModel>().ToPagedList(page, 10);
                    ViewBag.HospitalID = hospitalId;
                    ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                    ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                    return View(empty);
                }

                var balance = await _statisticsService.GetHospitalTestBalanceAsync(
                    hospitalId,
                    startDate,
                    endDate,
                    ct
                );

                var viewModel = balance.Select(b => new HospitalTestBalanceViewModel
                {
                    HospitalID = b.HospitalID,
                    HospitalName = b.HospitalName,
                    TestID = b.TestID,
                    TestName = b.TestName,
                    TotalSent = b.TotalSent,
                    TotalUsed = b.TotalUsed,
                    Month = b.Month
                }).ToList();

                var pagedList = viewModel.ToPagedList(page, 10);

                ViewBag.HospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(pagedList);
            }
            catch (Exception ex)
            {
                // Hata durumunda filtre sayfasına geri dön
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
