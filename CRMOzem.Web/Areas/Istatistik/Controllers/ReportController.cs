using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public IActionResult Index()
        {
            ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
            return View(new ReportFilterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ReportFilterViewModel model, int page = 1)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                return View(model);
            }

            try
            {
                var balance = await _statisticsService.GetHospitalTestBalanceAsync(
                    model.HospitalID,
                    model.StartDate,
                    model.EndDate
                );

                var viewModel = balance.Select(b => new HospitalTestBalanceViewModel
                {
                    HospitalID = b.HospitalID,
                    HospitalName = b.HospitalName,
                    TestID = b.TestID,
                    TestName = b.TestName,
                    TotalSent = b.TotalSent,
                    TotalUsed = b.TotalUsed,
                    Month = b.Month // burayı eklediğine emin ol!
                }).ToList();

                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.StartDate = model.StartDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = model.EndDate?.ToString("yyyy-MM-dd");

                // 🔹 Sayfalama: 10 kayıt
                var pagedList = viewModel.ToPagedList(page, 10);

                return View("Result", pagedList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

    }
}
