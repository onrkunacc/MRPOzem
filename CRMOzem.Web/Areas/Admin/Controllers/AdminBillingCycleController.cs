using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Yönetici,Muhasebe,Finans")]
    public class AdminBillingCycleController : Controller
    {
        private readonly IUserService _userService;
        private readonly IExpenseReportService _expenseReportService;

        public AdminBillingCycleController(IUserService userService, IExpenseReportService expenseReportService)
        {
            _userService = userService;
            _expenseReportService = expenseReportService;
        }

        public async Task<IActionResult> Index(string userId, int? year, int? month)
        {
            try
            {
                var users = await _userService.GetUsersByRoleAsync("Kullanıcı");
                var reports = new List<BillingCycleReportViewModel>();

                if (!string.IsNullOrEmpty(userId))
                {
                    reports = await _expenseReportService.GetUserBillingCycleReportsAsync(userId);

                    if (year.HasValue)
                        reports = reports.Where(r => r.Year == year.Value).ToList();

                    if (month.HasValue)
                        reports = reports.Where(r => r.Month == month.Value).ToList();
                }

                ViewBag.SelectedUserId = userId;
                ViewBag.Users = users.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.FullName
                }).ToList();
                ViewBag.SelectedYear = year;
                ViewBag.SelectedMonth = month;
                ViewBag.TotalAmount = reports.Sum(r => r.TotalAmount);

                return View(reports);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminBillingCycleController] HATA: {ex.Message}");
                TempData["Error"] = "Ekstre raporları yüklenirken bir hata oluştu.";
                return View(new List<BillingCycleReportViewModel>());
            }
        }
    }
}
