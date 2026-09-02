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

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik,Aplikasyon,Lojistik,İstatistikGiris")]
    public class ReportController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;
        private readonly ISutComparisonService _sutService;
        private readonly IKacAyYeterService _kayService;

        public ReportController(
            IStatisticsService statisticsService,
            ApplicationDbContext context,
            ISutComparisonService sutService,
            IKacAyYeterService kayService)
        {
            _statisticsService = statisticsService;
            _context = context;
            _sutService = sutService;
            _kayService = kayService;
        }

        // ═══════════════════════════════════════════════════════════
        // ANA RAPOR — Hastane bazlı kapsamlı analiz
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, CancellationToken ct)
        {
            try
            {
                await LoadHospitalsAsync(hospitalId);

                if (hospitalId == null || hospitalId <= 0)
                    return View(model: null);

                var vm = await _kayService.BuildAsync(hospitalId.Value, ct);
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Rapor yüklenemedi: " + ex.Message;
                await LoadHospitalsAsync(hospitalId);
                return View(model: null);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int hospitalId, CancellationToken ct)
        {
            try
            {
                var vm = await _kayService.BuildAsync(hospitalId, ct);

                if (vm.Rows.Count == 0)
                {
                    TempData["Warning"] = "Bu hastane için aktif test bulunamadı.";
                    return RedirectToAction(nameof(Index), new { hospitalId });
                }

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Rapor");
                var culture = new CultureInfo("tr-TR");

                // Başlık
                ws.Cell("A1").Value = $"{vm.HospitalName} — Stok ve Tüketim Raporu";
                ws.Cell("A1").Style.Font.Bold = true;
                ws.Cell("A1").Style.Font.FontSize = 14;
                ws.Range("A1:I1").Merge();

                ws.Cell("A2").Value = $"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}  •  Referans Ay: {vm.ReferenceMonthLabel}";
                ws.Cell("A2").Style.Font.Italic = true;
                ws.Cell("A2").Style.Font.FontColor = XLColor.Gray;
                ws.Range("A2:I2").Merge();

                // Tablo başlıkları
                var headers = new[] {
                    "Test Adı", "SUT Kodu", "Tip", "Toplam Gönderilen",
                    "Toplam Kullanılan", "Stok", $"Geçen Ay Tüketim",
                    "Kaç Ay Yeter", "Durum"
                };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(4, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Satırlar
                int row = 5;
                foreach (var r in vm.Rows)
                {
                    ws.Cell(row, 1).Value = r.TestName;
                    ws.Cell(row, 2).Value = r.SutCode ?? "-";
                    ws.Cell(row, 3).Value = r.GirisTipi.ToString();
                    ws.Cell(row, 4).Value = r.ToplamGonderilen;
                    ws.Cell(row, 5).Value = r.ToplamKullanilan;
                    ws.Cell(row, 6).Value = r.Stok;
                    ws.Cell(row, 7).Value = r.GecenAyTuketim;
                    ws.Cell(row, 8).Value = r.KacAyYeter.HasValue ? r.KacAyYeter.Value.ToString("N1", culture) : "—";
                    ws.Cell(row, 9).Value = r.DurumText;

                    var bgColor = r.Durum switch
                    {
                        StokDurum.Kritik => XLColor.FromArgb(254, 226, 226),
                        StokDurum.Uyari => XLColor.FromArgb(254, 243, 199),
                        StokDurum.Iyi => XLColor.FromArgb(209, 250, 229),
                        _ => XLColor.FromArgb(243, 244, 246)
                    };

                    ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = bgColor;
                    ws.Range(row, 1, row, 9).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    ws.Range(row, 1, row, 9).Style.Border.BottomBorderColor = XLColor.FromArgb(229, 231, 235);

                    ws.Cell(row, 9).Style.Font.Bold = true;
                    ws.Range(row, 4, row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    row++;
                }

                ws.Columns().AdjustToContents();
                ws.Column(1).Width = Math.Min(40, ws.Column(1).Width);

                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Rapor_{vm.HospitalName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream.ToArray(), contentType, fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Excel oluşturulamadı: " + ex.Message;
                return RedirectToAction(nameof(Index), new { hospitalId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int hospitalId, CancellationToken ct)
        {
            try
            {
                var vm = await _kayService.BuildAsync(hospitalId, ct);

                if (vm.Rows.Count == 0)
                {
                    TempData["Warning"] = "Bu hastane için aktif test bulunamadı.";
                    return RedirectToAction(nameof(Index), new { hospitalId });
                }

                QuestPDF.Settings.License = LicenseType.Community;
                var culture = new CultureInfo("tr-TR");

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4.Landscape());
                        page.PageColor(Colors.White);

                        page.Header().Column(col =>
                        {
                            col.Item().Text("Stok ve Tüketim Raporu").FontSize(16).SemiBold();
                            col.Item().Text(vm.HospitalName).FontSize(11).FontColor(Colors.Grey.Medium);
                            col.Item().Text($"Referans Ay: {vm.ReferenceMonthLabel}  •  Toplam: {vm.ToplamTest} test  •  Kritik: {vm.KritikSayi}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        page.Content().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                string[] h = { "Test", "SUT", "Tip", "Gönderilen", "Kullanılan", "Stok", "Geçen Ay", "Kaç Ay", "Durum" };
                                foreach (var t in h)
                                {
                                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text(t).FontSize(8.5f);
                                }
                            });

                            foreach (var r in vm.Rows)
                            {
                                var bg = r.Durum switch
                                {
                                    StokDurum.Kritik => Colors.Red.Lighten4,
                                    StokDurum.Uyari => Colors.Yellow.Lighten3,
                                    StokDurum.Iyi => Colors.Green.Lighten4,
                                    _ => Colors.Grey.Lighten4
                                };

                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).Text(r.TestName).FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).Text(r.SutCode ?? "-").FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).Text(r.GirisTipi.ToString()).FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).AlignRight().Text(r.ToplamGonderilen.ToString("N0", culture)).FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).AlignRight().Text(r.ToplamKullanilan.ToString("N0", culture)).FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).AlignRight().Text(r.Stok.ToString("N0", culture)).FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).AlignRight().Text(r.GecenAyTuketim.ToString("N0", culture)).FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).AlignRight().Text(r.KacAyYeter.HasValue ? r.KacAyYeter.Value.ToString("N1", culture) : "—").FontSize(8);
                                table.Cell().Element(c => DataCellStyle(c).Background(bg)).AlignCenter().Text(r.DurumText).FontSize(8).SemiBold();
                            }
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm} — Sayfa ").FontSize(8);
                            x.CurrentPageNumber().FontSize(8);
                            x.Span(" / ").FontSize(8);
                            x.TotalPages().FontSize(8);
                        });
                    });
                });

                var pdfBytes = document.GeneratePdf();
                var pdfName = $"Rapor_{vm.HospitalName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                return File(pdfBytes, "application/pdf", pdfName);

                static IContainer HeaderCellStyle(IContainer container)
                    => container.DefaultTextStyle(x => x.SemiBold())
                        .PaddingVertical(4).PaddingHorizontal(3)
                        .Background(Colors.Grey.Lighten3)
                        .BorderBottom(1).BorderColor(Colors.Grey.Medium);

                static IContainer DataCellStyle(IContainer container)
                    => container.PaddingVertical(3).PaddingHorizontal(3)
                        .BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "PDF oluşturulamadı: " + ex.Message;
                return RedirectToAction(nameof(Index), new { hospitalId });
            }
        }

        // ═══════════════════════════════════════════════════════════
        // SUT KARŞILAŞTIRMA — DEĞİŞMEDİ
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> SutKarsilastirma(int? hospitalId, int? year, int? month, int? ihaleYear, int? currentYear)
        {
            if (!hospitalId.HasValue || hospitalId.Value <= 0)
            {
                await LoadHospitalsAsync(null);
                ViewBag.DefaultYear = DateTime.Now.Year;
                ViewBag.DefaultMonth = DateTime.Now.Month;
                ViewBag.DefaultIhaleYear = DateTime.Now.Year - 1;
                ViewBag.DefaultCurrentYear = DateTime.Now.Year;
                return View("SutKarsilastirmaSelect");
            }

            int y = year ?? DateTime.Now.Year;
            int m = month ?? DateTime.Now.Month;
            int ihY = ihaleYear ?? (DateTime.Now.Year - 1);
            int curY = currentYear ?? DateTime.Now.Year;

            var exists = await _context.Hospitals.AsNoTracking().AnyAsync(x => x.HospitalID == hospitalId.Value);
            if (!exists)
            {
                await LoadHospitalsAsync(null);
                ViewBag.Error = "Seçilen hastane bulunamadı. Lütfen tekrar seçiniz.";
                ViewBag.DefaultYear = y;
                ViewBag.DefaultMonth = m;
                ViewBag.DefaultIhaleYear = ihY;
                ViewBag.DefaultCurrentYear = curY;
                return View("SutKarsilastirmaSelect");
            }

            var vm = await _sutService.BuildMonthlyAsync(hospitalId.Value, y, m, ihY, curY);
            return View("SutKarsilastirma", vm);
        }

        // ═══════════════════════════════════════════════════════════
        // Helper
        // ═══════════════════════════════════════════════════════════
        private async Task LoadHospitalsAsync(int? selectedHospitalId)
        {
            ViewBag.Hospitals = await _context.Hospitals.AsNoTracking()
                .OrderBy(x => x.HospitalName)
                .Select(x => new SelectListItem
                {
                    Value = x.HospitalID.ToString(),
                    Text = x.HospitalName,
                    Selected = selectedHospitalId.HasValue && x.HospitalID == selectedHospitalId.Value
                })
                .ToListAsync();
        }
    }
}