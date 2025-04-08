using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using X.PagedList.Extensions;

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

        public async Task<IActionResult> Index(string userId, int? year, string sortColumn = "MonthYear", string sortDirection = "desc", int page = 1)
        {
            var reports = string.IsNullOrEmpty(userId)
                ? await _reportService.GetAllReportsAsync()
                : await _reportService.GetReportsByUserIdAsync(userId);

            if (year.HasValue)
                reports = reports.Where(r => r.MonthYear.Year == year.Value).ToList();

            reports = sortColumn switch
            {
                "TotalAmount" => (sortDirection == "asc"
                    ? reports.OrderBy(r => r.TotalAmount)
                    : reports.OrderByDescending(r => r.TotalAmount)).ToList(),

                _ => (sortDirection == "asc"
                    ? reports.OrderBy(r => r.MonthYear)
                    : reports.OrderByDescending(r => r.MonthYear)).ToList(),
            };

            var availableYears = reports.Select(r => r.MonthYear.Year).Distinct().OrderByDescending(y => y).ToList();
            var pagedReports = reports.ToPagedList(page, 10);

            var users = await _userService.GetUsersByRoleAsync("Kullanıcı");

            var model = new AdminExpenseReportViewModel
            {
                PagedReports = pagedReports,
                Reports = reports,
                AvailableYears = availableYears,
                SelectedYear = year,
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
