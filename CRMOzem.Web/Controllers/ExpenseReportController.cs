using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Web.Controllers
{
    public class ExpenseReportController : Controller
    {
        private readonly IExpenseReportService _expenseService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ExpenseReportController(IExpenseReportService expenseService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _expenseService = expenseService;
            _userManager = userManager;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReport()
        {
            var user = await _userManager.GetUserAsync(User);
            await _expenseService.CreateReportAsync(user.Id, DateTime.Now);
            return RedirectToAction(nameof(MonthlyReports));
        }
        [HttpGet]
        public IActionResult AddExpense(int reportId)
        {
            var model = new ExpenseDetailViewModel
            {
                ReportId = reportId, // Kullanıcının mevcut raporuna eklenecek
                Date = DateTime.Now, // Varsayılan olarak bugünü ayarla
                PaymentMethod = (PaymentMethod)3 
            };
            return View(model);
        }

        // ================== MASRAF EKLE (MODAL POST) ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExpense(AddExpenseInputModel model, string submitAction)
        {
            ModelState.Remove("submitAction");
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "⚠️ Zorunlu alanları doldurun.";
                TempData["OpenAddModal"] = model.ReportId.ToString();
                return RedirectToAction(nameof(Details), new { id = model.ReportId });
            }

            try
            {
                var report = await _context.ExpenseReports
                                           .Include(r => r.ExpenseDetails)
                                           .FirstOrDefaultAsync(r => r.Id == model.ReportId);

                if (report == null)
                {
                    TempData["Error"] = "❌ Rapor bulunamadı.";
                    return RedirectToAction(nameof(MonthlyReports));
                }

                report.ExpenseDetails.Add(new ExpenseDetail
                {
                    ReportId = model.ReportId,
                    Date = model.Date,
                    Category = model.Category,
                    Description = model.Description,
                    PaymentMethod = model.PaymentMethod,
                    Amount = model.Amount
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ Masraf eklendi.";

                if (string.Equals(submitAction, "save_and_new", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["OpenAddModal"] = report.Id.ToString();
                    return RedirectToAction(nameof(Details), new { id = report.Id });
                }

                return RedirectToAction(nameof(Details), new { id = report.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Masraf eklenemedi: {ex.Message}";
                TempData["OpenAddModal"] = model.ReportId.ToString();
                return RedirectToAction(nameof(Details), new { id = model.ReportId });
            }
        }


        public async Task<IActionResult> ExportToExcel(int reportId)
        {
            var file = await _expenseService.ExportToExcelAsync(reportId);
            if (file == null)
                return NotFound("Masraf raporu bulunamadı.");

            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MasrafRaporu.xlsx");
        }

        // ================== DETAY ==================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var report = await _context.ExpenseReports
                                       .Include(r => r.ExpenseDetails)
                                       .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (report == null)
            {
                TempData["Error"] = "Masraf raporu bulunamadı.";
                return RedirectToAction(nameof(MonthlyReports));
            }

            ViewBag.CalculatedTotal = report.ExpenseDetails.Sum(e => e.Amount);
            ViewBag.AddExpenseModel = new AddExpenseInputModel { ReportId = report.Id };
            return View("Details", report);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
            {
                return NotFound("Masraf Bulunamadı.");
            }

            var model = new ExpenseDetailViewModel
            {
                Id = expense.Id,
                Date = expense.Date,
                Category = expense.Category,
                Description = expense.Description,
                PaymentMethod = expense.PaymentMethod,
                Amount = expense.Amount
            };
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(ExpenseDetailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.Remove("Category");
                ModelState.Remove("Description");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                                                  .Select(e => e.ErrorMessage)
                                                  .ToList();
                    return BadRequest(new { message = "Model validation failed", errors });
                }
            }

            var expense = await _expenseService.GetExpenseByIdAsync(model.Id);
            if (expense == null)
            {
                return NotFound("Masraf Bulunamadı.");
            }

           
            expense.Category = model.Category;
            expense.Description = model.Description;
            expense.PaymentMethod = model.PaymentMethod;
            expense.Amount = model.Amount;
            expense.Date = model.Date;

            await _expenseService.UpdateExpenseAsync(expense);
            return RedirectToAction("Details", "ExpenseReport", new { id = expense.ReportId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteExpense(int expenseId)
        {
            var exp = await _expenseService.GetExpenseByIdAsync(expenseId);
            if (exp == null) return NotFound("Silinmek istenen masraf bulunamadı.");

            var reportId = exp.ReportId;

            var success = await _expenseService.DeleteExpenseAsync(expenseId);
            if (!success) return NotFound("Silme işlemi başarısız.");

            return RedirectToAction(nameof(Details), new { id = reportId }); // ✅ doğru sayfaya dönüş
        }

        //[HttpPost]
        //public async Task<IActionResult> UpdateCycleInline(int id, int? overrideMonth, int? overrideYear)
        //{
        //    try
        //    {
        //        var expense = await _expenseService.GetExpenseByIdAsync(id);
        //        if (expense == null)
        //            return NotFound("Masraf bulunamadı.");

        //        expense.OverrideCycleMonth = overrideMonth;
        //        expense.OverrideCycleYear = overrideYear;
        //        await _expenseService.UpdateExpenseAsync(expense);

        //        var monthName = overrideMonth.HasValue
        //            ? CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(overrideMonth.Value)
        //            : "Otomatik";

        //        return Ok(new
        //        {
        //            success = true,
        //            month = overrideMonth,
        //            year = overrideYear,
        //            monthName
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Ekstre dönemi güncellenirken hata oluştu.",
        //            error = ex.Message
        //        });
        //    }
        //}

        [HttpGet]
        public async Task<IActionResult> PrintReport(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var report = await _context.ExpenseReports
                                       .Include(r => r.ExpenseDetails)
                                       .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
            if (report == null) return NotFound("Masraf raporu bulunamadı.");

            using var ms = new MemoryStream();
            var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 50, 50, 50, 50);
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var fTitle = new iTextSharp.text.Font(bf, 18, iTextSharp.text.Font.BOLD);
            var fHeader = new iTextSharp.text.Font(bf, 12, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
            var fReg = new iTextSharp.text.Font(bf, 10);
            var fRed = new iTextSharp.text.Font(bf, 14, iTextSharp.text.Font.BOLD, BaseColor.RED);

            // --- HEADER ---
            PdfPTable header = new PdfPTable(2) { WidthPercentage = 100 };
            header.SetWidths(new float[] { 50f, 50f });

            PdfPCell logoCell = new PdfPCell { Border = PdfPCell.NO_BORDER };
            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tinydash-master", "light", "assets", "images", "logo.jpeg");
            if (System.IO.File.Exists(logoPath))
            {
                var logo = iTextSharp.text.Image.GetInstance(logoPath);
                logo.ScaleAbsolute(100f, 75f);
                logoCell.AddElement(logo);
            }

            PdfPCell userCell = new PdfPCell(new Phrase(
                $"{user?.FullName ?? "Kullanıcı"}\n{report.MonthYear:MMMM yyyy}",
                new iTextSharp.text.Font(bf, 12, iTextSharp.text.Font.BOLD)))
            {
                Border = PdfPCell.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };

            header.AddCell(logoCell);
            header.AddCell(userCell);
            doc.Add(header);

            // --- TITLE ---
            var title = new iTextSharp.text.Paragraph($"{report.MonthYear:MMMM yyyy} Masraf Raporu", fTitle)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            doc.Add(title);

            // --- TABLE ---
            PdfPTable table = new PdfPTable(5) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 15f, 20f, 35f, 15f, 15f });

            string[] headers = { "Tarih", "Kategori", "Açıklama", "Ödeme Yöntemi", "Tutar (₺)" };
            foreach (var h in headers)
            {
                var c = new PdfPCell(new Phrase(h, fHeader))
                {
                    BackgroundColor = new BaseColor(0, 102, 204),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 5
                };
                table.AddCell(c);
            }

            foreach (var e in report.ExpenseDetails.OrderBy(x => x.Date))
            {
                table.AddCell(new Phrase(e.Date.ToString("dd.MM.yyyy"), fReg));
                table.AddCell(new Phrase(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(e.Category ?? ""), fReg));
                table.AddCell(new Phrase(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(e.Description ?? ""), fReg));
                table.AddCell(new Phrase(e.PaymentMethod.ToString(), fReg));
                table.AddCell(new Phrase($"{e.Amount:N2} ₺", fReg));
            }
            doc.Add(table);

            // --- TOTAL ---
            decimal total = report.ExpenseDetails.Sum(x => x.Amount);
            PdfPTable totalT = new PdfPTable(1) { WidthPercentage = 30, HorizontalAlignment = Element.ALIGN_RIGHT };
            PdfPCell totalC = new PdfPCell(new Phrase($"Toplam Tutar: {total:N2} ₺", fRed))
            {
                Border = PdfPCell.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingTop = 10
            };
            totalT.AddCell(totalC);
            doc.Add(totalT);

            // --- SIGNATURE ---
            var sig = new iTextSharp.text.Paragraph("\n\nİmza :..................", fReg) { SpacingBefore = 30 };
            doc.Add(sig);

            doc.Close();
            byte[] pdfBytes = ms.ToArray();

            // 📌 Tarayıcıda yeni sekmede açılması için "inline" ayarı
            Response.Headers.Add("Content-Disposition", $"inline; filename=MasrafRaporu_{report.MonthYear:yyyy_MM}.pdf");
            return File(pdfBytes, "application/pdf");
        }





        //Aylık deneme raporu 
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateReportViewModel
            {
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month
            });
        }

        // Yeni rapor oluşturma (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReportViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var user = await _userManager.GetUserAsync(User);
                var exists = await _context.ExpenseReports
                                           .AnyAsync(r => r.UserId == user.Id &&
                                                          r.MonthYear.Year == model.Year &&
                                                          r.MonthYear.Month == model.Month);
                if (exists)
                {
                    TempData["Warning"] = "Bu aya ait rapor zaten mevcut.";
                    return RedirectToAction(nameof(MonthlyReports), new { year = model.Year });
                }

                var report = new ExpenseReport
                {
                    UserId = user.Id,
                    MonthYear = new DateTime(model.Year, model.Month, 1),
                    ExpenseDetails = new List<ExpenseDetail>()
                };

                _context.ExpenseReports.Add(report);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Rapor başarıyla oluşturuldu.";
                return RedirectToAction(nameof(MonthlyReports), new { year = model.Year });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Rapor oluşturulurken bir hata oluştu: {ex.Message}";
                return View(model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> AddToReport(int reportId)
        {
            var report = await _context.ExpenseReports.FindAsync(reportId);
            if (report == null)
                return NotFound();

            var model = new ExpenseDetailViewModel
            {
                ReportId = report.Id,
                Date = DateTime.Now,
                PaymentMethod = PaymentMethod.Kart
            };

            return View("AddToReport", model);
        }

        [HttpPost]
        public async Task<IActionResult> AddToReport(ExpenseDetailViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var report = await _context.ExpenseReports
                .Include(r => r.ExpenseDetails)
                .FirstOrDefaultAsync(r => r.Id == model.ReportId);

            if (report == null)
                return NotFound();

            // 🔍 Tarih kontrolü: aynı ay mı?
            if (model.Date.Month != report.MonthYear.Month || model.Date.Year != report.MonthYear.Year)
            {
                TempData["Error"] = $"⚠️ Masraf tarihi ({model.Date:MMMM yyyy}) ile rapor tarihi ({report.MonthYear:MMMM yyyy}) uyuşmuyor.";
                return View(model); // ya da uyarı view'ı
            }

            report.ExpenseDetails.Add(new ExpenseDetail
            {
                ReportId = model.ReportId,
                Date = model.Date,
                Description = model.Description,
                Category = model.Category,
                PaymentMethod = model.PaymentMethod,
                Amount = model.Amount
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Masraf başarıyla eklendi!";
            return RedirectToAction("InputDetails", new { year = report.MonthYear.Year, month = report.MonthYear.Month });
        }

        // ================== ANA LİSTE (YENİ) ==================
        [HttpGet]
        public async Task<IActionResult> MonthlyReports(int? year)
        {
            var user = await _userManager.GetUserAsync(User);

            var q = _context.ExpenseReports
                            .Include(r => r.ExpenseDetails)
                            .Where(r => r.UserId == user.Id);

            if (year.HasValue)
                q = q.Where(r => r.MonthYear.Year == year.Value);

            var reports = await q.OrderByDescending(r => r.MonthYear).ToListAsync();

            ViewBag.SelectedYear = year;
            ViewBag.Years = await _context.ExpenseReports
                                          .Where(r => r.UserId == user.Id)
                                          .Select(r => r.MonthYear.Year)
                                          .Distinct()
                                          .OrderByDescending(y => y)
                                          .ToListAsync();

            ViewBag.AddExpenseModel = new AddExpenseInputModel(); // modal model
            ViewBag.CreateReportModel = new CreateReportViewModel
            {
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month
            };

            return View("MonthlyReports", reports);
        }
        // ================== YENİ RAPOR (MODAL POST) ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReportModal(CreateReportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["OpenCreateModal"] = "1";
                TempData["Error"] = "Ay/Yıl seçimini kontrol edin.";
                return RedirectToAction(nameof(MonthlyReports), new { year = model.Year });
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);

                bool exists = await _context.ExpenseReports
                    .AnyAsync(r => r.UserId == user.Id &&
                                   r.MonthYear.Year == model.Year &&
                                   r.MonthYear.Month == model.Month);

                if (exists)
                {
                    TempData["Warning"] = "Bu aya ait rapor zaten mevcut.";
                    return RedirectToAction(nameof(MonthlyReports), new { year = model.Year });
                }

                _context.ExpenseReports.Add(new ExpenseReport
                {
                    UserId = user.Id,
                    MonthYear = new DateTime(model.Year, model.Month, 1),
                    ExpenseDetails = new List<ExpenseDetail>()
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Rapor oluşturuldu.";
                return RedirectToAction(nameof(MonthlyReports), new { year = model.Year });
            }
            catch (Exception ex)
            {
                TempData["OpenCreateModal"] = "1";
                TempData["Error"] = $"Rapor oluşturulamadı: {ex.Message}";
                return RedirectToAction(nameof(MonthlyReports), new { year = model.Year });
            }
        }

        [HttpGet]
        public async Task<IActionResult> InputDetails(int year, int month)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                var report = await _context.ExpenseReports
                    .Include(r => r.ExpenseDetails)
                    .FirstOrDefaultAsync(r => r.UserId == user.Id && r.MonthYear.Month == month && r.MonthYear.Year == year);

                if (report == null)
                    return NotFound();

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Detaylar yüklenemedi: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
