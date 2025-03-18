using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using iTextSharp.text.pdf;
using iTextSharp.text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRMOzem.Core.ViewModels;

namespace CRMOzem.Web.Controllers
{
    public class BillingCycleReportController : Controller
    {
        private readonly IExpenseReportService _expenseService;
        private readonly UserManager<ApplicationUser> _userManager;
        public BillingCycleReportController(IExpenseReportService expenseService,UserManager<ApplicationUser> userManager)
        {
             _expenseService = expenseService;
            _userManager = userManager;
        }
        public async  Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var reports = await _expenseService.GetUserBillingCycleReportsAsync(user.Id);

            if (!reports.Any())
            {
                Console.WriteLine("🚨 Hiç ekstre raporu bulunamadı!");
            }

            return View(reports);
        }
        public async Task<IActionResult> Details(int year, int month)
        {
            var user = await _userManager.GetUserAsync(User);
            var reports = await _expenseService.GetUserBillingCycleReportsAsync(user.Id);

            var selectedReport = reports.FirstOrDefault(r => r.Year == year && r.Month == month);
            if (selectedReport == null)
                return NotFound("Bu aya ait ekstre raporu bulunamadı.");

            return View(selectedReport);
        }

        // ✅ Ekstre PDF Raporu Oluşturma
        public async Task<IActionResult> PrintBillingCycleReport()
        {
            var user = await _userManager.GetUserAsync(User);
            var reports = await _expenseService.GetUserBillingCycleReportsAsync(user.Id);
            decimal totalAmount = reports.Sum(r => r.TotalAmount);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                iTextSharp.text.Document document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // 📌 Türkçe karakter desteği için Arial font ekleniyor
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                iTextSharp.text.Font fontTitle = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                iTextSharp.text.Font fontRegular = new iTextSharp.text.Font(baseFont, 10);
                iTextSharp.text.Font fontRedBold = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.BOLD, BaseColor.RED);

                // 📌 Başlık ekleniyor
                iTextSharp.text.Paragraph title = new iTextSharp.text.Paragraph($"Ekstre Masraf Raporu: {DateTime.UtcNow:MMMM yyyy}", fontTitle);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                document.Add(new iTextSharp.text.Paragraph($"Toplam: {totalAmount:N2} ₺", fontRedBold));

                // 📌 Tablo başlıkları oluşturuluyor
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

                // 📌 Masraf detayları ekleniyor
                foreach (var report in reports)
                {
                    foreach (var expense in report.ExpenseDetails)
                    {
                        table.AddCell(new Phrase(expense.Date.ToString("dd.MM.yyyy"), fontRegular));
                        table.AddCell(new Phrase(expense.Category, fontRegular));
                        table.AddCell(new Phrase(expense.Description, fontRegular));
                        table.AddCell(new Phrase(expense.PaymentMethod.ToString(), fontRegular));
                        table.AddCell(new Phrase($"{expense.Amount:N2} ₺", fontRegular));
                    }
                }

                document.Add(table);
                document.Close();

                byte[] pdfBytes = memoryStream.ToArray();
                return File(pdfBytes, "application/pdf", "EkstreMasrafRaporu.pdf");
            }
        }
    }
}
