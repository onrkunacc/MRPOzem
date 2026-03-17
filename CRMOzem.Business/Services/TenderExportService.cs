using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class TenderExportService : ITenderExportService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenderCalculatorService _calculator;

    public TenderExportService(ApplicationDbContext context, ITenderCalculatorService calculator)
    {
        _context = context;
        _calculator = calculator;
    }

    public async Task<byte[]> ExportExcelDailyAsync(DateTime date)
    {
        var tenders = await LoadDaily(date);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Ihaleler");

        ws.Cell(1, 1).Value = "İhale Adı";
        ws.Cell(1, 2).Value = "Kayıt No";
        ws.Cell(1, 3).Value = "Tarih";
        ws.Cell(1, 4).Value = "Süre(Ay)";
        ws.Cell(1, 5).Value = "Test";
        ws.Cell(1, 6).Value = "Ek";
        ws.Cell(1, 7).Value = "Personel";
        ws.Cell(1, 8).Value = "Genel Toplam";

        int r = 2;
        foreach (var t in tenders)
        {
            ws.Cell(r, 1).Value = t.Name;
            ws.Cell(r, 2).Value = t.RegistrationNumber;
            ws.Cell(r, 3).Value = t.TenderDate;
            ws.Cell(r, 4).Value = t.DurationMonths;
            ws.Cell(r, 5).Value = (double)t.TotalTestCost;
            ws.Cell(r, 6).Value = (double)t.TotalExtraCost;
            ws.Cell(r, 7).Value = (double)t.TotalPersonnelCost;
            ws.Cell(r, 8).Value = (double)t.OverallCost;
            r++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportPdfDailyAsync(DateTime date)
    {
        var tenders = await LoadDaily(date);

        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Content().Column(col =>
                {
                    col.Item().Text($"İhale Gün Sonu Raporu — {date:dd.MM.yyyy}").FontSize(16).SemiBold();
                    col.Item().PaddingTop(10);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("İhale").SemiBold();
                            h.Cell().Text("Kayıt No").SemiBold();
                            h.Cell().AlignRight().Text("Tarih").SemiBold();
                            h.Cell().AlignRight().Text("Toplam").SemiBold();
                        });

                        foreach (var t in tenders)
                        {
                            table.Cell().Text(t.Name ?? "");
                            table.Cell().Text(t.RegistrationNumber ?? "");
                            table.Cell().AlignRight().Text(t.TenderDate.ToString("dd.MM.yyyy"));
                            table.Cell().AlignRight().Text(t.OverallCost.ToString("C2"));
                        }
                    });

                    var grand = tenders.Sum(x => x.OverallCost);
                    col.Item().PaddingTop(12).AlignRight().Text($"Gün Toplamı: {grand:C2}").SemiBold();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private async Task<List<CRMOzem.Core.Models.Tender>> LoadDaily(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);

        var tenders = await _context.Tenders
            .Include(t => t.Tests)
            .Include(t => t.ExtraCosts)
            .Include(t => t.Personnels)
            .Where(t => t.TenderDate >= start && t.TenderDate < end)
            .ToListAsync();

        foreach (var t in tenders) _calculator.Calculate(t);
        return tenders;
    }
}