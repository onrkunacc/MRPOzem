using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    public class ExpenseReportController : Controller
    {
        private readonly IExpenseReportService _expenseService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExpenseReportController(IExpenseReportService expenseService, UserManager<ApplicationUser> userManager)
        {
            _expenseService = expenseService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {

            var user = await _userManager.GetUserAsync(User);
            var reports = await _expenseService.GetUserReportsAsync(user.Id);
            return View(reports);
        }

        [HttpPost]
        public async Task<IActionResult> CreateReport()
        {
            var user = await _userManager.GetUserAsync(User);
            await _expenseService.CreateReportAsync(user.Id, DateTime.Now);
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public IActionResult AddExpense(int reportId)
        {
            var model = new ExpenseDetailViewModel
            {
                ReportId = reportId, // Kullanıcının mevcut raporuna eklenecek
                Date = DateTime.Now // Varsayılan olarak bugünü ayarla
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddExpense(ExpenseDetailViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var success = await _expenseService.AddExpenseAsync(model, user.Id);

                if (success)
                    return RedirectToAction("Index");

                ModelState.AddModelError("", "Masraf eklenemedi.");
            }
            return View(model);
        }

        public async Task<IActionResult> ExportToExcel(int reportId)
        {
            var file = await _expenseService.ExportToExcelAsync(reportId);
            if (file == null)
                return NotFound("Masraf raporu bulunamadı.");

            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MasrafRaporu.xlsx");
        }

        public async Task<IActionResult> Details(int id)
        {
            var report = await _expenseService.GetReportByIdAsync(id);
            if (report == null)
            {
                return NotFound("Masraf raporu bulunamadı.");
            }
            return View(report);
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
            if (expense == null) {
                return NotFound("Masraf Bulunamadı.");            
            }
            expense.Amount = model.Amount;

            await _expenseService.UpdateExpenseAsync(expense);
            return RedirectToAction("Details", "ExpenseReport", new { id = expense.ReportId });
        }

        public async Task<IActionResult> PrintReport(int reportId)
        {
            var report = await _expenseService.GetReportByIdAsync(reportId);
            if (report == null)
                return NotFound("Masraf raporu bulunamadı.");

            using (MemoryStream memoryStream = new MemoryStream())
            {
                iTextSharp.text.Document document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // **Türkçe Karakter Desteği için Arial Font Kullanımı**
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                iTextSharp.text.Font fontTitle = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                iTextSharp.text.Font fontRegular = new iTextSharp.text.Font(baseFont, 10);
                iTextSharp.text.Font fontRedBold = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.BOLD, BaseColor.RED);

                // **Başlık Tablosu (Logo + Kullanıcı Bilgisi)**
                PdfPTable headerTable = new PdfPTable(2);
                headerTable.WidthPercentage = 100;
                headerTable.SetWidths(new float[] { 50f, 50f });

                // **Şirket Logosu Sadece Buraya Eklendi**
                PdfPCell logoCell = new PdfPCell();
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tinydash-master", "light", "assets", "images", "logo.jpeg");
                if (System.IO.File.Exists(logoPath))
                {
                    iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(100f, 75f);
                    logoCell.AddElement(logo);
                }
                logoCell.Border = PdfPCell.NO_BORDER;

                // **Kullanıcı Adı ve Tarih Sağ Üste**
                var user = await _userManager.GetUserAsync(User);
                PdfPCell userInfoCell = new PdfPCell(new Phrase($"{user?.FullName ?? "Bilinmeyen Kullanıcı"}\n{report.MonthYear:MM.yyyy}",
                    new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD)))
                {
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Border = PdfPCell.NO_BORDER
                };

                headerTable.AddCell(logoCell);
                headerTable.AddCell(userInfoCell);
                document.Add(headerTable); 

                // **Başlık**
                iTextSharp.text.Paragraph title = new iTextSharp.text.Paragraph($"{report.MonthYear.ToString("MMMM yyyy")} Masraf Raporu", fontTitle);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                document.Add(title);

                // **Tablo Başlıkları**
                PdfPTable table = new PdfPTable(5);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 15f, 20f, 35f, 15f, 15f });

                string[] headers = { "Tarih", "Kategori", "Açıklama", "Ödeme Yöntemi", "Tutar (₺)" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, fontHeader))
                    {
                        BackgroundColor = new BaseColor(0, 102, 204),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                // **Tablo İçeriği - Tarih Küçükten Büyüğe Sıralandı**
                foreach (var expense in report.ExpenseDetails.OrderBy(e => e.Date))
                {
                    table.AddCell(new Phrase(expense.Date.ToString("dd.MM.yyyy"), fontRegular));
                    table.AddCell(new Phrase(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(expense.Category), fontRegular));
                    table.AddCell(new Phrase(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(expense.Description), fontRegular));
                    table.AddCell(new Phrase(expense.PaymentMethod.ToString(), fontRegular));
                    table.AddCell(new Phrase($"{expense.Amount:N2} ₺", fontRegular));
                }

                document.Add(table);

                // **Toplam Tutar Sağ Alt Köşeye Taşındı**
                PdfPTable totalAmountTable = new PdfPTable(1);
                totalAmountTable.WidthPercentage = 30;
                totalAmountTable.HorizontalAlignment = Element.ALIGN_RIGHT;
                PdfPCell totalAmountCell = new PdfPCell(new Phrase($"Toplam Tutar: {report.TotalAmount:N2} ₺", fontRedBold))
                {
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Border = PdfPCell.NO_BORDER,
                    PaddingTop = 10
                };

                iTextSharp.text.Paragraph signature = new iTextSharp.text.Paragraph("\n\nİmza :..................", fontRegular);
                signature.Alignment = Element.ALIGN_LEFT;
                signature.SpacingBefore = 30;
                document.Add(signature);

                totalAmountTable.AddCell(totalAmountCell);
                document.Add(totalAmountTable);

                document.Close();

                byte[] pdfBytes = memoryStream.ToArray();
                return File(pdfBytes, "application/pdf");
            }
        }
    }
}
