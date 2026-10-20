using System.Diagnostics;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    [Authorize(Roles="Kullanýcý")]
    public class HomeController : Controller
    {
        private readonly IExpenseReportService _expenseService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(IExpenseReportService expenseService, UserManager<ApplicationUser> userManager)
        {
            _expenseService = expenseService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var activeReport = await _expenseService.GetCurrentMonthExpenseAsync(user.Id);
            ViewBag.TotalExpense = activeReport;

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> TestSyncStok(
            [FromServices] IFizikiSayimSyncService sync,
            [FromServices] IFizikiSayimApiClient api,
            int? hospitalId,
            CancellationToken ct)
        {
            // Önce ping
            var ping = await api.PingAsync(ct);
            if (!ping) return Json(new { success = false, message = "Ping baþarýsýz — API key veya URL yanlýþ" });

            // Sync et
            var sonuc = await sync.SyncAsync(hospitalId, ct);
            return Json(sonuc);
        }

        [HttpGet]
        public async Task<IActionResult> TestEslesme(
            [FromServices] IFizikiSayimSyncService sync,
            CancellationToken ct)
        {
            var rapor = await sync.TestEslesmeAsync(ct);
            return Json(rapor);
        }


       

        [HttpGet]
        public async Task<IActionResult> GetTotalExpense()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            var activeReport = await _expenseService.GetCurrentMonthExpenseAsync(user.Id);
            decimal totalExpense = activeReport;

            return Json(new { totalAmount = totalExpense });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}
