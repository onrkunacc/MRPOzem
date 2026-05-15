using CRMOzem.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Yönetici")]
    public class AdminController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;

        public AdminController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index(string periyot = "ay", CancellationToken ct = default)
        {
            try
            {
                var vm = await _dashboardService.GetDashboardAsync(periyot, ct);
                ViewBag.SelectedPeriyot = periyot;
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Dashboard yüklenirken hata: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}