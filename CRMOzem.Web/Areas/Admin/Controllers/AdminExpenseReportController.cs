using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Yönetici,Muhasebe,Finans")]
    public class AdminExpenseReportController : Controller
    {
        private readonly IReportService _reportService;

        public AdminExpenseReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> Index(AdminExpenseFilterVm filter, CancellationToken ct)
        {
            filter ??= new AdminExpenseFilterVm();
            if (filter.Page < 1) filter.Page = 1;

            var vm = await _reportService.GetAdminIndexAsync(filter, pageSize: 20, ct: ct);
            return View(vm);
        }

        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var vm = await _reportService.GetAdminDetailsAsync(id, ct);
            if (vm == null)
            {
                TempData["Error"] = "Rapor bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            return View(vm);
        }

        public async Task<IActionResult> Charts(string? userId, int? year, CancellationToken ct)
        {
            var vm = await _reportService.GetAdminChartsAsync(userId, year, ct);
            return View(vm);
        }
    }
}