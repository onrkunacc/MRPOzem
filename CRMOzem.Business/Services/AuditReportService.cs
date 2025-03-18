using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Business.Services
{
    public class AuditReportService : IAuditReportService
    {
        private readonly ApplicationDbContext _context;

        public AuditReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<List<Invoices>> GetFilteredInvoices(
            int? cariGrupId,
            List<int>? cariIds,
            string? il,
            string? ihaleNo,
            int? donem,
            int? baslangicAy,
            int? bitisAy)
        {
            var query = _context.Invoices
                .Include(i => i.Cari)
                .ThenInclude(c => c.CariGrubu)
                .AsQueryable();

            if (cariGrupId.HasValue)
                query = query.Where(i => i.Cari.CariGrubuId == cariGrupId.Value);

            if (cariIds != null && cariIds.Any())
                query = query.Where(i => cariIds.Contains(i.CariId));

            if (!string.IsNullOrEmpty(il))
                query = query.Where(i => i.Cari.Il == il);

            if (!string.IsNullOrEmpty(ihaleNo))
                query = query.Where(i => i.Cari.IhaleNo == ihaleNo);

            if (donem.HasValue)
                query = query.Where(i => i.Donemi == donem.Value);

            if (baslangicAy.HasValue && bitisAy.HasValue)
                query = query.Where(i => i.Ay >= baslangicAy && i.Ay <= bitisAy);

            return await query.ToListAsync();
        }

        /// <summary>
        /// Excel Raporu oluşturur
        /// </summary>
        public async Task<(MemoryStream?, string?)> GenerateExcelReport(
            int? cariGrupId, List<int>? cariIds, string? il, string? ihaleNo,
            int? donem, int? baslangicAy, int? bitisAy)
        {
            try
            {
                var invoices = await GetFilteredInvoices(cariGrupId, cariIds, il, ihaleNo, donem, baslangicAy, bitisAy);
                if (!invoices.Any()) return (null, "Belirtilen kriterlere uygun fatura bulunamadı.");

                var excelStream = new MemoryStream();
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Fatura Raporu");

                    // 📌 Başlıklar
                    var headers = new[] { "Cari Kodu", "Unvan", "Cari Grubu", "İl", "İhale No", "Ay", "Dönem", "Tutar (₺)" };
                    for (int col = 0; col < headers.Length; col++)
                    {
                        worksheet.Cell(1, col + 1).Value = headers[col];
                        worksheet.Cell(1, col + 1).Style.Font.Bold = true;
                        worksheet.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.LightSkyBlue;
                    }

                    var ayAdlari = new[]
                    {
                        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
                    };

                    int row = 2;
                    decimal toplamTutar = 0;
                    foreach (var invoice in invoices.OrderBy(i => i.Cari.CariKodu).ThenBy(i => i.Ay))
                    {
                        worksheet.Cell(row, 1).Value = invoice.Cari.CariKodu;
                        worksheet.Cell(row, 2).Value = invoice.Cari.Unvan;
                        worksheet.Cell(row, 3).Value = invoice.Cari.CariGrubu?.GrupAdi ?? "Bilinmiyor";
                        worksheet.Cell(row, 4).Value = invoice.Cari.Il;
                        worksheet.Cell(row, 5).Value = invoice.Cari.IhaleNo;
                        worksheet.Cell(row, 6).Value = ayAdlari[invoice.Ay - 1];
                        worksheet.Cell(row, 7).Value = invoice.Donemi;
                        worksheet.Cell(row, 8).Style.NumberFormat.Format = "₺ #,##0.00";
                        worksheet.Cell(row, 8).Value = invoice.Tutar;

                        toplamTutar += invoice.Tutar;
                        row++;
                    }

                    // 📌 Toplam Tutar
                    worksheet.Cell(row, 7).Value = "Toplam Tutar:";
                    worksheet.Cell(row, 8).Value = toplamTutar;
                    worksheet.Cell(row, 8).Style.Font.Bold = true;
                    worksheet.Cell(row, 8).Style.Fill.BackgroundColor = XLColor.LightGray;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "₺ #,##0.00";

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(excelStream);
                }

                excelStream.Position = 0;
                return (excelStream, null);
            }
            catch (Exception ex)
            {
                return (null, $"Excel raporu oluşturulurken hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// PDF Raporu oluşturur
        /// </summary>
        public async Task<(MemoryStream?, string?)> GeneratePdfReport(
            int? cariGrupId, List<int>? cariIds, string? il, string? ihaleNo,
            int? donem, int? baslangicAy, int? bitisAy)
        {
            try
            {
                var invoices = await GetFilteredInvoices(cariGrupId, cariIds, il, ihaleNo, donem, baslangicAy, bitisAy);
                if (!invoices.Any()) return (null, "Belirtilen kriterlere uygun fatura bulunamadı.");

                var pdfStream = new MemoryStream();
                Document document = new Document(PageSize.A4, 20, 20, 20, 20);
                PdfWriter writer = PdfWriter.GetInstance(document, pdfStream);
                writer.CloseStream = false;

                document.Open();

                // ✅ Türkçe karakter desteği için Unicode Font ekleyelim
                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font headerFont = new Font(bf, 12, Font.BOLD);
                Font cellFont = new Font(bf, 10, Font.NORMAL);

                // 📌 Başlık
                var title = new Paragraph("📄 Fatura Raporu\n\n", headerFont) { Alignment = Element.ALIGN_CENTER };
                document.Add(title);

                // 📌 PDF Tablo Tanımlama
                PdfPTable table = new PdfPTable(6) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 15, 30, 20, 20, 20, 20 });

                // 📌 Tablo Başlıklarını Kalınlaştırma
                string[] headers = { "Cari Kodu", "Unvan", "İl", "İhale No", "Tutar (₺)", "Dönem" };

                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = new BaseColor(192, 192, 192), // Gri arka plan
                        Padding = 5,
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };
                    table.AddCell(cell);
                }

                // 📌 Fatura Bilgileri
                decimal toplamTutar = 0;
                foreach (var invoice in invoices.OrderBy(i => i.Cari.CariKodu).ThenBy(i => i.Ay))
                {
                    table.AddCell(new PdfPCell(new Phrase(invoice.Cari.CariKodu, cellFont)));
                    table.AddCell(new PdfPCell(new Phrase(invoice.Cari.Unvan, cellFont)));
                    table.AddCell(new PdfPCell(new Phrase(invoice.Cari.Il, cellFont)));
                    table.AddCell(new PdfPCell(new Phrase(invoice.Cari.IhaleNo, cellFont)));
                    table.AddCell(new PdfPCell(new Phrase(invoice.Tutar.ToString("C"), cellFont)));
                    table.AddCell(new PdfPCell(new Phrase(invoice.Donemi.ToString(), cellFont)));

                    toplamTutar += invoice.Tutar;
                }

                // 📌 TOPLAM TUTAR SATIRI
                PdfPCell emptyCell = new PdfPCell(new Phrase("Toplam Tutar:", headerFont))
                {
                    Colspan = 4,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    BackgroundColor = new BaseColor(220, 220, 220),
                    Padding = 5
                };
                table.AddCell(emptyCell);

                PdfPCell totalCell = new PdfPCell(new Phrase(toplamTutar.ToString("C"), headerFont))
                {
                    Colspan = 2,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    BackgroundColor = new BaseColor(255, 228, 181), // Açık turuncu
                    Padding = 5
                };
                table.AddCell(totalCell);

                document.Add(table);
                document.Close();
                pdfStream.Position = 0;

                return (pdfStream, null);
            }
            catch (Exception ex)
            {
                return (null, $"PDF raporu oluşturulurken hata oluştu: {ex.Message}");
            }
        }
    }
}
