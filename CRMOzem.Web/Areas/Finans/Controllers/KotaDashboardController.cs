using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace CRMOzem.Web.Areas.Finans.Controllers
{
    [Area("Finans")]
    [Authorize(Roles = "Yönetici,KotaTakip")]
    public class KotaDashboardController : Controller
    {
        private readonly IKotaDashboardService _service;
        private readonly IKotaService _kotaService;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public KotaDashboardController(IKotaDashboardService service, IKotaService kotaService)
        {
            _service = service;
            _kotaService = kotaService;
        }

        // ═══════════════════════════════════════════════════════════
        // MAIN DASHBOARD — artık KotaDashboardVm döndürüyor
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? startDate,
            DateTime? endDate,
            string? bolge,
            KotaDurum? durum,
            TedarikciGrup? tedarikciGrup,
            int? kotaId,
            CancellationToken ct)
        {
            var filter = new KotaDashboardFilterVm
            {
                StartDate = startDate,
                EndDate = endDate,
                Bolge = bolge,
                Durum = durum,
                TedarikciGrup = tedarikciGrup,
                KotaId = kotaId
            };

            var vm = await _service.BuildAsync(filter, ct);
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        // EXCEL EXPORT
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            DateTime? startDate, DateTime? endDate,
            string? bolge, KotaDurum? durum,
            TedarikciGrup? tedarikciGrup, int? kotaId,
            CancellationToken ct)
        {
            var filter = new KotaDashboardFilterVm
            {
                StartDate = startDate,
                EndDate = endDate,
                Bolge = bolge,
                Durum = durum,
                TedarikciGrup = tedarikciGrup,
                KotaId = kotaId
            };
            var vm = await _service.BuildAsync(filter, ct);

            using var wb = new XLWorkbook();

            var ws1 = wb.Worksheets.Add("Özet");
            OzetSayfasi(ws1, vm);

            var ws2 = wb.Worksheets.Add("Kotalar");
            KotalarSayfasi(ws2, vm);

            var ws3 = wb.Worksheets.Add("Aylık Trend");
            AylikTrendSayfasi(ws3, vm);

            var ws4 = wb.Worksheets.Add("Bölge Özet");
            BolgeOzetSayfasi(ws4, vm);

            if (vm.YillikOzet.Any())
            {
                var ws5 = wb.Worksheets.Add("Yıllık Özet");
                YillikOzetSayfasi(ws5, vm);
            }

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"KotaDashboard_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private void OzetSayfasi(IXLWorksheet ws, KotaDashboardVm vm)
        {
            ws.Cell("A1").Value = "KOTA DASHBOARD - ÖZET RAPOR";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Cell("A1").Style.Font.FontColor = XLColor.White;
            ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            ws.Range("A1:D1").Merge();
            ws.Row(1).Height = 30;

            ws.Cell("A2").Value = $"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";
            ws.Cell("A2").Style.Font.Italic = true;
            ws.Cell("A2").Style.Font.FontColor = XLColor.Gray;
            ws.Range("A2:D2").Merge();

            ws.Cell("A3").Value = $"Dönem: {vm.Filter.PeriyotText}";
            ws.Cell("A3").Style.Font.Italic = true;
            ws.Range("A3:D3").Merge();

            if (vm.GuncelUsdKur.HasValue)
            {
                ws.Cell("A4").Value = $"Güncel USD/TL: {vm.GuncelUsdKur.Value:N4} ({vm.GuncelKurTarihi:dd.MM.yyyy})";
                ws.Cell("A4").Style.Font.Italic = true;
                ws.Range("A4:D4").Merge();
            }

            int row = 6;
            ws.Cell(row, 1).Value = "KPI ÖZETİ";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            ws.Range(row, 1, row, 4).Merge();
            row++;

            AddKpi(ws, ref row, "Toplam Kota Sayısı", vm.ToplamKotaSayisi.ToString(), XLColor.FromArgb(59, 130, 246));
            AddKpi(ws, ref row, "Aktif Kota", vm.AktifKotaSayisi.ToString(), XLColor.FromArgb(16, 185, 129));
            AddKpi(ws, ref row, "Onayda / Bekleyen", vm.OnaydaBekleyenSayisi.ToString(), XLColor.FromArgb(245, 158, 11));
            AddKpi(ws, ref row, "Yeni Başlayacak", vm.YeniBaslayacakSayisi.ToString(), XLColor.FromArgb(139, 92, 246));

            row++;
            ws.Cell(row, 1).Value = "MALİ ÖZET (USD)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            ws.Range(row, 1, row, 4).Merge();
            row++;

            AddKpi(ws, ref row, "Toplam Proforma", $"${vm.ToplamProformaUsd:N2}", XLColor.FromArgb(59, 130, 246));
            AddKpi(ws, ref row, "Toplam Gider", $"${vm.ToplamGiderUsd:N2}", XLColor.FromArgb(239, 68, 68));
            AddKpi(ws, ref row, "Toplam Gelir", $"${vm.ToplamGelirUsd:N2}", XLColor.FromArgb(16, 185, 129));
            AddKpi(ws, ref row, "Kalan Proforma", $"${vm.KalanUsd:N2}", XLColor.FromArgb(245, 158, 11));
            AddKpi(ws, ref row, "Kar / Zarar",
                $"${vm.KarZararUsd:N2}",
                vm.KarZararUsd >= 0 ? XLColor.FromArgb(16, 185, 129) : XLColor.FromArgb(239, 68, 68));

            row++;
            ws.Cell(row, 1).Value = "GİDER DAĞILIMI";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            ws.Range(row, 1, row, 4).Merge();
            row++;

            AddKpi(ws, ref row, "Abbott Gider", $"${vm.AbbottGiderUsd:N2}", XLColor.FromArgb(220, 38, 38));
            AddKpi(ws, ref row, "3rd Party Gider", $"${vm.ThirdPartyGiderUsd:N2}", XLColor.FromArgb(245, 158, 11));
            AddKpi(ws, ref row, "Diğer Gider", $"${vm.DigerGiderUsd:N2}", XLColor.FromArgb(139, 92, 246));

            ws.Columns().AdjustToContents();
            ws.Column(1).Width = 25;
            ws.Column(2).Width = 20;
        }

        private void AddKpi(IXLWorksheet ws, ref int row, string label, string value, XLColor color)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontColor = color;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Range(row, 1, row, 4).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 4).Style.Border.BottomBorderColor = XLColor.FromArgb(229, 231, 235);
            row++;
        }

        private void KotalarSayfasi(IXLWorksheet ws, KotaDashboardVm vm)
        {
            ws.Cell("A1").Value = "KOTA DETAY LİSTESİ";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A1").Style.Font.FontColor = XLColor.White;
            ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            ws.Range("A1:J1").Merge();
            ws.Row(1).Height = 25;

            // KotaSatirlari'nı kullan — tüm finansal alanlar burada
            var headers = new[] { "Kota Kodu", "İş Adı", "Sorumlu", "Bölge", "Durum",
                                   "Proforma (USD)", "Abbott (USD)", "3rd Party (USD)",
                                   "Toplam Gider (USD)", "Gelir (USD)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 4;
            foreach (var k in vm.KotaSatirlari)
            {
                ws.Cell(row, 1).Value = k.KotaKodu ?? "-";
                ws.Cell(row, 2).Value = k.IsAdi;
                ws.Cell(row, 3).Value = k.Sorumlu ?? "-";
                ws.Cell(row, 4).Value = k.Bolge ?? "-";
                ws.Cell(row, 5).Value = k.Durum.ToString();
                ws.Cell(row, 6).Value = k.ProformaUsd;
                ws.Cell(row, 7).Value = k.AbbottGiderUsd;
                ws.Cell(row, 8).Value = k.ThirdPartyGiderUsd;
                ws.Cell(row, 9).Value = k.ToplamGiderUsd;
                ws.Cell(row, 10).Value = k.GelirUsd;

                for (int c = 6; c <= 10; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "$#,##0.00";

                // Kar/Zarar rengi gelir hücresine
                ws.Cell(row, 10).Style.Font.FontColor = k.BrutFarkUsd >= 0
                    ? XLColor.FromArgb(16, 185, 129) : XLColor.FromArgb(239, 68, 68);

                ws.Range(row, 1, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 10).Style.Border.BottomBorderColor = XLColor.FromArgb(229, 231, 235);

                row++;
            }

            // TOPLAM satırı
            if (vm.KotaSatirlari.Any())
            {
                ws.Cell(row, 1).Value = "TOPLAM";
                ws.Cell(row, 6).Value = vm.KotaSatirlari.Sum(k => k.ProformaUsd);
                ws.Cell(row, 7).Value = vm.KotaSatirlari.Sum(k => k.AbbottGiderUsd);
                ws.Cell(row, 8).Value = vm.KotaSatirlari.Sum(k => k.ThirdPartyGiderUsd);
                ws.Cell(row, 9).Value = vm.KotaSatirlari.Sum(k => k.ToplamGiderUsd);
                ws.Cell(row, 10).Value = vm.KotaSatirlari.Sum(k => k.GelirUsd);

                for (int c = 6; c <= 10; c++)
                {
                    ws.Cell(row, c).Style.NumberFormat.Format = "$#,##0.00";
                    ws.Cell(row, c).Style.Font.Bold = true;
                }
                ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                ws.Range(row, 1, row, 10).Style.Font.Bold = true;
            }

            ws.Columns().AdjustToContents();
        }

        private void AylikTrendSayfasi(IXLWorksheet ws, KotaDashboardVm vm)
        {
            ws.Cell("A1").Value = "AYLIK GİDER-GELİR TRENDİ";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A1").Style.Font.FontColor = XLColor.White;
            ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            ws.Range("A1:D1").Merge();

            var headers = new[] { "Ay", "Gider (USD)", "Gelir (USD)", "Kar/Zarar (USD)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 4;
            foreach (var t in vm.AylikTrend)
            {
                ws.Cell(row, 1).Value = t.AyLabel;
                ws.Cell(row, 2).Value = t.GiderUsd;
                ws.Cell(row, 3).Value = t.GelirUsd;
                ws.Cell(row, 4).Value = t.KarZararUsd;

                ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 3).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 4).Style.Font.FontColor = t.KarZararUsd >= 0
                    ? XLColor.FromArgb(16, 185, 129) : XLColor.FromArgb(239, 68, 68);

                ws.Range(row, 1, row, 4).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 4).Style.Border.BottomBorderColor = XLColor.FromArgb(229, 231, 235);
                row++;
            }

            ws.Cell(row, 1).Value = "TOPLAM";
            ws.Cell(row, 2).Value = vm.AylikTrend.Sum(t => t.GiderUsd);
            ws.Cell(row, 3).Value = vm.AylikTrend.Sum(t => t.GelirUsd);
            ws.Cell(row, 4).Value = vm.AylikTrend.Sum(t => t.KarZararUsd);
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 3).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";

            ws.Columns().AdjustToContents();
        }

        private void BolgeOzetSayfasi(IXLWorksheet ws, KotaDashboardVm vm)
        {
            ws.Cell("A1").Value = "BÖLGE BAZLI ÖZET";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A1").Style.Font.FontColor = XLColor.White;
            ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            ws.Range("A1:E1").Merge();

            var headers = new[] { "Bölge", "Kota Sayısı", "Toplam Proforma (USD)", "Toplam Gider (USD)", "Kalan (USD)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 4;
            foreach (var b in vm.BolgeOzet)
            {
                ws.Cell(row, 1).Value = b.Bolge;
                ws.Cell(row, 2).Value = b.KotaSayisi;
                ws.Cell(row, 3).Value = b.ToplamProformaUsd;
                ws.Cell(row, 4).Value = b.ToplamGiderUsd;
                ws.Cell(row, 5).Value = b.ToplamProformaUsd - b.ToplamGiderUsd;

                ws.Cell(row, 3).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";

                ws.Range(row, 1, row, 5).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 5).Style.Border.BottomBorderColor = XLColor.FromArgb(229, 231, 235);
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void YillikOzetSayfasi(IXLWorksheet ws, KotaDashboardVm vm)
        {
            ws.Cell("A1").Value = "YILLIK KARŞILAŞTIRMA";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A1").Style.Font.FontColor = XLColor.White;
            ws.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            ws.Range("A1:G1").Merge();

            var headers = new[] { "Yıl", "Abbott Gider", "3rd Party Gider", "Diğer Gider", "Toplam Gider", "Gelir", "Kar/Zarar" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 4;
            foreach (var y in vm.YillikOzet)
            {
                ws.Cell(row, 1).Value = y.Yil;
                ws.Cell(row, 2).Value = y.AbbottGiderUsd;
                ws.Cell(row, 3).Value = y.ThirdPartyGiderUsd;
                ws.Cell(row, 4).Value = y.DigerGiderUsd;
                ws.Cell(row, 5).Value = y.GiderUsd;
                ws.Cell(row, 6).Value = y.GelirUsd;
                ws.Cell(row, 7).Value = y.KarZararUsd;

                for (int c = 2; c <= 7; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "$#,##0.00";

                ws.Cell(row, 7).Style.Font.FontColor = y.KarZararUsd >= 0
                    ? XLColor.FromArgb(16, 185, 129) : XLColor.FromArgb(239, 68, 68);
                ws.Cell(row, 7).Style.Font.Bold = true;

                ws.Range(row, 1, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ═══════════════════════════════════════════════════════════
        // PDF EXPORT
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            DateTime? startDate, DateTime? endDate,
            string? bolge, KotaDurum? durum,
            TedarikciGrup? tedarikciGrup, int? kotaId,
            CancellationToken ct)
        {
            var filter = new KotaDashboardFilterVm
            {
                StartDate = startDate,
                EndDate = endDate,
                Bolge = bolge,
                Durum = durum,
                TedarikciGrup = tedarikciGrup,
                KotaId = kotaId
            };
            var vm = await _service.BuildAsync(filter, ct);

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4.Landscape());
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("KOTA DASHBOARD").FontSize(18).Bold().FontColor(Colors.Indigo.Medium);
                                c.Item().Text("Yönetim Raporu").FontSize(10).FontColor(Colors.Grey.Medium);
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Dönem: {vm.Filter.PeriyotText}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"Rapor: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);
                                if (vm.GuncelUsdKur.HasValue)
                                    c.Item().Text($"1 USD = {vm.GuncelUsdKur.Value:N4} TL").FontSize(9).FontColor(Colors.Grey.Medium);
                            });
                        });
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Indigo.Medium);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            KpiKutu(row, "Toplam Proforma", $"${vm.ToplamProformaUsd:N0}", Colors.Blue.Medium);
                            KpiKutu(row, "Toplam Gider", $"${vm.ToplamGiderUsd:N0}", Colors.Red.Medium);
                            KpiKutu(row, "Toplam Gelir", $"${vm.ToplamGelirUsd:N0}", Colors.Green.Medium);
                            KpiKutu(row, "Kar / Zarar", $"${vm.KarZararUsd:N0}",
                                vm.KarZararUsd >= 0 ? Colors.Green.Darken1 : Colors.Red.Darken1);
                        });

                        col.Item().PaddingTop(5).Text($"Kota Sayısı: {vm.ToplamKotaSayisi}  •  Aktif: {vm.AktifKotaSayisi}  •  Onayda/Bekleyen: {vm.OnaydaBekleyenSayisi}  •  Yeni: {vm.YeniBaslayacakSayisi}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);

                        col.Item().PaddingTop(10).Text("Gider Dağılımı").FontSize(12).Bold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Grup").FontSize(9);
                                h.Cell().Element(HeaderStyle).AlignRight().Text("Tutar (USD)").FontSize(9);
                                h.Cell().Element(HeaderStyle).AlignRight().Text("Oran").FontSize(9);
                            });
                            var toplam = vm.ToplamGiderUsd > 0 ? vm.ToplamGiderUsd : 1;
                            t.Cell().Element(CellStyle).Text("Abbott").FontSize(9);
                            t.Cell().Element(CellStyle).AlignRight().Text($"${vm.AbbottGiderUsd:N2}").FontSize(9);
                            t.Cell().Element(CellStyle).AlignRight().Text($"%{(vm.AbbottGiderUsd / toplam * 100):N1}").FontSize(9);
                            t.Cell().Element(CellStyle).Text("3rd Party").FontSize(9);
                            t.Cell().Element(CellStyle).AlignRight().Text($"${vm.ThirdPartyGiderUsd:N2}").FontSize(9);
                            t.Cell().Element(CellStyle).AlignRight().Text($"%{(vm.ThirdPartyGiderUsd / toplam * 100):N1}").FontSize(9);
                            t.Cell().Element(CellStyle).Text("Diğer").FontSize(9);
                            t.Cell().Element(CellStyle).AlignRight().Text($"${vm.DigerGiderUsd:N2}").FontSize(9);
                            t.Cell().Element(CellStyle).AlignRight().Text($"%{(vm.DigerGiderUsd / toplam * 100):N1}").FontSize(9);
                        });

                        if (vm.AylikTrend.Any())
                        {
                            col.Item().PaddingTop(10).Text("Aylık Trend").FontSize(12).Bold();
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                });
                                t.Header(h =>
                                {
                                    h.Cell().Element(HeaderStyle).Text("Ay").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Gider").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Gelir").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Kar/Zarar").FontSize(9);
                                });
                                foreach (var tr in vm.AylikTrend.Where(x => x.GiderUsd > 0 || x.GelirUsd > 0))
                                {
                                    t.Cell().Element(CellStyle).Text(tr.AyLabel).FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${tr.GiderUsd:N0}").FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${tr.GelirUsd:N0}").FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${tr.KarZararUsd:N0}").FontSize(9)
                                        .FontColor(tr.KarZararUsd >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                }
                            });
                        }

                        if (vm.BolgeOzet.Any())
                        {
                            col.Item().PaddingTop(10).Text("Bölge Bazlı Özet").FontSize(12).Bold();
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1.5f);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1.2f);
                                    c.RelativeColumn(1.2f);
                                });
                                t.Header(h =>
                                {
                                    h.Cell().Element(HeaderStyle).Text("Bölge").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignCenter().Text("Kota #").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Proforma").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Gider").FontSize(9);
                                });
                                foreach (var b in vm.BolgeOzet)
                                {
                                    t.Cell().Element(CellStyle).Text(b.Bolge).FontSize(9);
                                    t.Cell().Element(CellStyle).AlignCenter().Text(b.KotaSayisi.ToString()).FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${b.ToplamProformaUsd:N0}").FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${b.ToplamGiderUsd:N0}").FontSize(9);
                                }
                            });
                        }

                        if (vm.EnYukluKotalar.Any())
                        {
                            col.Item().PaddingTop(10).Text("En Yüklü 5 Kota").FontSize(12).Bold();
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(0.7f);
                                });
                                t.Header(h =>
                                {
                                    h.Cell().Element(HeaderStyle).Text("Kota").FontSize(9);
                                    h.Cell().Element(HeaderStyle).Text("Bölge").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Proforma").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignRight().Text("Gider").FontSize(9);
                                    h.Cell().Element(HeaderStyle).AlignCenter().Text("%").FontSize(9);
                                });
                                foreach (var k in vm.EnYukluKotalar)
                                {
                                    t.Cell().Element(CellStyle).Text(k.IsAdi).FontSize(9);
                                    t.Cell().Element(CellStyle).Text(k.Bolge ?? "-").FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${k.ProformaUsd:N0}").FontSize(9);
                                    t.Cell().Element(CellStyle).AlignRight().Text($"${k.GiderUsd:N0}").FontSize(9);
                                    t.Cell().Element(CellStyle).AlignCenter().Text($"%{k.Yuzde:N1}").FontSize(9)
                                        .FontColor(k.Yuzde > 80 ? Colors.Red.Darken2 : k.Yuzde > 50 ? Colors.Orange.Darken2 : Colors.Green.Darken2);
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Özem Tıbbi Cihazlar  •  Kota Dashboard  •  Sayfa ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            var fileName = $"KotaDashboard_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(pdfBytes, "application/pdf", fileName);

            static void KpiKutu(RowDescriptor row, string label, string value, string color)
            {
                row.RelativeItem().PaddingRight(5).Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(value).FontSize(14).Bold().FontColor(color);
                });
            }

            static IContainer HeaderStyle(IContainer c)
                => c.Background(Colors.Indigo.Lighten4).Padding(5).BorderBottom(1).BorderColor(Colors.Indigo.Medium);

            static IContainer CellStyle(IContainer c)
                => c.Padding(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
        }

        // ═══════════════════════════════════════════════════════════
        // KOTA DETAY MODAL (partial)
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> KotaDetayPartial(int id, CancellationToken ct)
        {
            var detay = await _service.GetKotaDetayAsync(id, ct);
            if (detay == null) return NotFound();
            return PartialView("_KotaDetayModal", detay);
        }
    }
}