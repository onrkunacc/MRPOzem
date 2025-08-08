using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CRMOzem.Web.Controllers
{
    public class ExpenseDetailController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpenseDetailController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Add(AddExpenseInputModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("Details", "ExpenseReport", new { year = DateTime.Now.Year, month = DateTime.Now.Month });

            try
            {
                var report = await _context.ExpenseReports.FindAsync(model.ReportId);
                if (report == null) return NotFound();

                bool mismatch = model.Date.Month != report.MonthYear.Month || model.Date.Year != report.MonthYear.Year;

                if (mismatch && !model.UserConfirmed)
                {
                    TempData["ConfirmMessage"] = $"⚠️ {report.MonthYear:MMMM yyyy} raporuna {model.Date:MMMM yyyy} tarihli masraf ekleniyor. Devam edilsin mi?";
                    TempData["PendingExpense"] = JsonConvert.SerializeObject(model);
                    return RedirectToAction("ConfirmMismatch");
                }

                _context.ExpenseDetails.Add(new ExpenseDetail
                {
                    ReportId = model.ReportId,
                    Date = model.Date,
                    Description = model.Description,
                    Amount = model.Amount,
                    Category = model.Category,
                    PaymentMethod = model.PaymentMethod
                });

                await _context.SaveChangesAsync();

                return RedirectToAction("Details", "ExpenseReport", new { year = report.MonthYear.Year, month = report.MonthYear.Month });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Masraf eklenemedi: {ex.Message}";
                return RedirectToAction("Details", "ExpenseReport", new { year = DateTime.Now.Year, month = DateTime.Now.Month });
            }
        }

        [HttpGet]
        public IActionResult ConfirmMismatch()
        {
            if (!TempData.ContainsKey("PendingExpense")) return RedirectToAction("Index", "Home");

            var model = JsonConvert.DeserializeObject<AddExpenseInputModel>((string)TempData["PendingExpense"]);
            ViewBag.ConfirmMessage = TempData["ConfirmMessage"];
            return View(model);
        }
    }
}
