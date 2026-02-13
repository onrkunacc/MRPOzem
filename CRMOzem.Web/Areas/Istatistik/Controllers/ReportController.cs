using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using X.PagedList.Extensions;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik")]
    public class ReportController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public ReportController(IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Hospitals = new SelectList(
                await _context.Hospitals
                    .AsNoTracking()
                    .OrderBy(x => x.HospitalName)
                    .ToListAsync(ct),
                "HospitalID",
                "HospitalName"
            );

            return View(new ReportFilterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ReportFilterViewModel model, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals
                        .AsNoTracking()
                        .OrderBy(x => x.HospitalName)
                        .ToListAsync(ct),
                    "HospitalID",
                    "HospitalName",
                    model.HospitalID
                );

                return View(model);
            }

            // POST → GET: aynı filtreyi Result için query string'e taşı.
            return RedirectToAction("Result", new
            {
                hospitalId = model.HospitalID,
                startDate = model.StartDate?.ToString("yyyy-MM-dd"),
                endDate = model.EndDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public async Task<IActionResult> Result(
            int hospitalId,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            CancellationToken ct = default)
        {
            try
            {
                var data = await GetReportDataAsync(hospitalId, startDate, endDate, ct);

                var pagedList = data.ToPagedList(page, 10);

                ViewBag.HospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(pagedList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // EXCEL EXPORT
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            int hospitalId,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken ct = default)
        {
            var data = await GetReportDataAsync(hospitalId, startDate, endDate, ct);

            if (!data.Any())
            {
                TempData["Warning"] = "Seçili filtrelere göre kayıt bulunamadı.";
                return RedirectToAction("Result", new
                {
                    hospitalId,
                    startDate = startDate?.ToString("yyyy-MM-dd"),
                    endDate = endDate?.ToString("yyyy-MM-dd")
                });
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Rapor");

            // Başlık satırı
            ws.Cell("A1").Value = "Hastane";
            ws.Cell("B1").Value = "Ay";
            ws.Cell("C1").Value = "Test Adı";
            ws.Cell("D1").Value = "Gönderilen";
            ws.Cell("E1").Value = "Kullanılan";
            ws.Cell("F1").Value = "Kalan";

            var headerRange = ws.Range("A1:F1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // Veri satırları
            var row = 2;
            var culture = new CultureInfo("tr-TR");
            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = item.HospitalName;
                ws.Cell(row, 2).Value = item.Month.ToString("MMMM yyyy", culture);
                ws.Cell(row, 3).Value = item.TestName;
                ws.Cell(row, 4).Value = item.TotalSent;
                ws.Cell(row, 5).Value = item.TotalUsed;
                ws.Cell(row, 6).Value = item.Remaining;
                row++;
            }

            // Toplam satırı
            ws.Cell(row, 3).Value = "Toplam";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 4).FormulaA1 = $"SUM(D2:D{row - 1})";
            ws.Cell(row, 5).FormulaA1 = $"SUM(E2:E{row - 1})";
            ws.Cell(row, 6).FormulaA1 = $"SUM(F2:F{row - 1})";
            ws.Range(row, 3, row, 6).Style.Font.Bold = true;
            ws.Range(row, 3, row, 6).Style.Border.TopBorder = XLBorderStyleValues.Thin;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Rapor_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(stream.ToArray(), contentType, fileName);
        }

        // PDF EXPORT
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            int hospitalId,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken ct = default)
        {
            var data = await GetReportDataAsync(hospitalId, startDate, endDate, ct);

            if (!data.Any())
            {
                TempData["Warning"] = "Seçili filtrelere göre kayıt bulunamadı.";
                return RedirectToAction("Result", new
                {
                    hospitalId,
                    startDate = startDate?.ToString("yyyy-MM-dd"),
                    endDate = endDate?.ToString("yyyy-MM-dd")
                });
            }

            QuestPDF.Settings.License = LicenseType.Community; // Lisans zorunlu ayar.

            var culture = new CultureInfo("tr-TR");
            var hospitalName = data.First().HospitalName;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Test Raporu").FontSize(16).SemiBold();
                            col.Item().Text(hospitalName).FontSize(10).FontColor(Colors.Grey.Medium);
                            col.Item().Text(text =>
                            {
                                text.Span("Tarih Aralığı: ")
                                    .SemiBold()
                                    .FontSize(9);

                                text.Span($"{startDate?.ToString("dd.MM.yyyy") ?? "-"} - {endDate?.ToString("dd.MM.yyyy") ?? "-"}")
                                    .FontSize(9);
                            });
                        });
                        row.ConstantItem(60).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(8);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Ay
                            columns.RelativeColumn(4); // Test Adı
                            columns.RelativeColumn(2); // Gönderilen
                            columns.RelativeColumn(2); // Kullanılan
                            columns.RelativeColumn(2); // Kalan
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Ay");
                            header.Cell().Element(HeaderCellStyle).Text("Test Adı");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Gönderilen");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Kullanılan");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Kalan");
                        });

                        // Rows
                        foreach (var item in data)
                        {
                            table.Cell().Element(DataCellStyle).Text(item.Month.ToString("MMMM yyyy", culture));
                            table.Cell().Element(DataCellStyle).Text(item.TestName);
                            table.Cell().Element(DataCellStyle).AlignRight().Text(item.TotalSent.ToString("N0", culture));
                            table.Cell().Element(DataCellStyle).AlignRight().Text(item.TotalUsed.ToString("N0", culture));
                            table.Cell().Element(container =>
                            {
                                var color = item.Remaining < 0 ? Colors.Red.Medium : Colors.Green.Medium;
                                DataCellStyle(container).AlignRight().Text(item.Remaining.ToString("N0", culture)).FontColor(color);
                            });
                        }

                        // Footer / totals
                        var totalSent = data.Sum(x => x.TotalSent);
                        var totalUsed = data.Sum(x => x.TotalUsed);
                        var totalRemaining = data.Sum(x => x.Remaining);

                        table.Cell().ColumnSpan(2).Element(FooterCellStyle).Text("Toplam");
                        table.Cell().Element(FooterCellStyle).AlignRight().Text(totalSent.ToString("N0", culture));
                        table.Cell().Element(FooterCellStyle).AlignRight().Text(totalUsed.ToString("N0", culture));
                        table.Cell().Element(container =>
                        {
                            var color = totalRemaining < 0 ? Colors.Red.Medium : Colors.Green.Medium;
                            FooterCellStyle(container).AlignRight().Text(totalRemaining.ToString("N0", culture)).FontColor(color);
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sayfa ").FontSize(9);
                        x.CurrentPageNumber().FontSize(9);
                        x.Span(" / ").FontSize(9);
                        x.TotalPages().FontSize(9);
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            var pdfName = $"Rapor_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(pdfBytes, "application/pdf", pdfName);

            static IContainer HeaderCellStyle(IContainer container)
                => container.DefaultTextStyle(x => x.SemiBold())
                    .PaddingVertical(4)
                    .PaddingHorizontal(3)
                    .Background(Colors.Grey.Lighten3)
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten1);

            static IContainer DataCellStyle(IContainer container)
                => container.PaddingVertical(3)
                    .PaddingHorizontal(3)
                    .BorderBottom(0.5f)
                    .BorderColor(Colors.Grey.Lighten3);

            static IContainer FooterCellStyle(IContainer container)
                => container.PaddingVertical(4)
                    .PaddingHorizontal(3)
                    .BorderTop(1)
                    .BorderColor(Colors.Grey.Lighten1)
                    .DefaultTextStyle(x => x.SemiBold());
        }

        // Ortak veri üretici
        private async Task<List<HospitalTestBalanceViewModel>> GetReportDataAsync(
            int hospitalId,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken ct)
        {
            if (hospitalId <= 0)
                return new List<HospitalTestBalanceViewModel>();

            var balance = await _statisticsService.GetHospitalTestBalanceAsync(
                hospitalId,
                startDate,
                endDate,
                ct
            );

            return balance.Select(b => new HospitalTestBalanceViewModel
            {
                HospitalID = b.HospitalID,
                HospitalName = b.HospitalName,
                TestID = b.TestID,
                TestName = b.TestName,
                TotalSent = b.TotalSent,
                TotalUsed = b.TotalUsed,
                Month = b.Month
            }).ToList();
        }
    }
}