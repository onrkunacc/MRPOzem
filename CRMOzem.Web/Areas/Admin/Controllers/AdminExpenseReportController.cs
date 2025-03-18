using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles ="Yönetici,Muhasebe,Finans")]
    public class AdminExpenseReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IUserService _userService;
        private readonly IExpenseReportService _expenseReportService;

        public AdminExpenseReportController(IReportService reportService, IUserService userService,IExpenseReportService expenseReportService)
        {
            _reportService = reportService;
            _userService = userService;
            _expenseReportService = expenseReportService;
        }

        public async Task<IActionResult> Index(string userId)
        {
            var reports = string.IsNullOrEmpty(userId)
                ? await _reportService.GetAllReportsAsync()
                : await _reportService.GetReportsByUserIdAsync(userId);

            var users = await _userService.GetUsersByRoleAsync("Kullanıcı");

            var model = new AdminExpenseReportViewModel
            {
                Reports = reports,
                Users = users.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.FullName
                }).ToList()
            };

            return View(model);
        }

        
        public async Task<IActionResult> Details(int id)
        {
            var report = await _expenseReportService.GetReportByIdAsync(id);
            if (report == null) return NotFound();
            return View(report);
        }
        public async Task<IActionResult> Charts(string userId = null)
        {
            var monthlyExpenses = await _expenseReportService.GetMonthlyExpensesAsync(userId);
            var users = await _userService.GetUsersByRoleAsync("Kullanıcı");

            var model = new AdminExpenseReportViewModel
            {
                MonthlyExpenses = monthlyExpenses,  // Hata giderildi
                Users = users.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.FullName
                }).ToList(),
                SelectedUserId = userId
            };

            return View(model);
        }


    }
}
