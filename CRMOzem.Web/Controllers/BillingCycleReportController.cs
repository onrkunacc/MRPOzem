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

        // Ekstre PDF Raporu Oluşturma
        public async Task<IActionResult> PrintBillingCycleReport(int year, int month)
        {
            var user = await _userManager.GetUserAsync(User);
            var reports = await _expenseService.GetUserBillingCycleReportsAsync(user.Id);

            Console.WriteLine($"🟢 {reports.Count} adet ekstre raporu bulundu.");

            foreach (var r in reports)
            {
                Console.WriteLine($"📅 {r.Year}-{r.Month}: {r.TotalAmount} ₺");
            }

            var selectedReport = reports.FirstOrDefault(r => r.Year == year && r.Month == month);
            if (selectedReport == null)
            {
                Console.WriteLine("🔴 Tam eşleşme bulunamadı, benzerleri kontrol ediliyor...");
                selectedReport = reports.FirstOrDefault(r => r.Year == year);
                if (selectedReport != null)
                    Console.WriteLine($"🟡 Eşleşen yıl bulundu, fakat ay farklı: {selectedReport.Month}");
            }
            using (MemoryStream memoryStream = new MemoryStream())
            {
                iTextSharp.text.Document document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // **Türkçe karakter desteği için Arial font**
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

                // **Şirket Logosu**
                PdfPCell logoCell = new PdfPCell();
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tinydash-master", "light", "assets", "images", "logo.jpeg");
                if (System.IO.File.Exists(logoPath))
                {
                    iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(100f, 75f);
                    logoCell.AddElement(logo);
                }
                logoCell.Border = PdfPCell.NO_BORDER;

                // **Kullanıcı Adı ve Tarih**
                PdfPCell userInfoCell = new PdfPCell(new Phrase($"{user?.FullName ?? "Bilinmeyen Kullanıcı"}\n{selectedReport.MonthName} {selectedReport.Year}",
                    new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD)))
                {
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Border = PdfPCell.NO_BORDER
                };

                headerTable.AddCell(logoCell);
                headerTable.AddCell(userInfoCell);
                document.Add(headerTable);

                // **Başlık**
                iTextSharp.text.Paragraph title = new iTextSharp.text.Paragraph($"Ekstre Masraf Raporu: {selectedReport.MonthName} {selectedReport.Year}", fontTitle);
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

                // **Tablo İçeriği - Tarih sıralaması**
                foreach (var expense in selectedReport.ExpenseDetails
                            .Where(e => e.Date != null)
                            .OrderBy(e => e.Date))
                {
                    table.AddCell(new Phrase(expense.Date.ToString("dd.MM.yyyy"), fontRegular));
                    table.AddCell(new Phrase(expense.Category, fontRegular));
                    table.AddCell(new Phrase(expense.Description, fontRegular));
                    table.AddCell(new Phrase(expense.PaymentMethod.ToString(), fontRegular));
                    table.AddCell(new Phrase($"{expense.Amount:N2} ₺", fontRegular));
                }

                document.Add(table);

                // **Toplam Tutar Sağ Alt Köşe**
                PdfPTable totalAmountTable = new PdfPTable(1);
                totalAmountTable.WidthPercentage = 30;
                totalAmountTable.HorizontalAlignment = Element.ALIGN_RIGHT;
                PdfPCell totalAmountCell = new PdfPCell(new Phrase($"Toplam Tutar: {selectedReport.TotalAmount:N2} ₺", fontRedBold))
                {
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Border = PdfPCell.NO_BORDER,
                    PaddingTop = 10
                };

                // **İmza Alanı**
                iTextSharp.text.Paragraph signature = new iTextSharp.text.Paragraph("\n\nİmza :..................", fontRegular);
                signature.Alignment = Element.ALIGN_LEFT;
                signature.SpacingBefore = 30;
                document.Add(signature);

                totalAmountTable.AddCell(totalAmountCell);
                document.Add(totalAmountTable);

                document.Close();

                byte[] pdfBytes = memoryStream.ToArray();

                // **Yeni sekmede açmak için PDF'yi inline olarak döndür**
                return File(pdfBytes, "application/pdf");
            }
        }

    }
}
