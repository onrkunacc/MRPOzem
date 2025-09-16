using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class FileParserService : IFileParserService
    {
        private readonly ApplicationDbContext _context;

        public FileParserService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Excel Parse
        public async Task<List<StatisticsPreviewItem>> ParseExcelAsync(IFormFile file)
        {
            var previewItems = new List<StatisticsPreviewItem>();
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet?.Dimension == null) return previewItems;

            int rowCount = worksheet.Dimension.End.Row;

            for (int row = 2; row <= rowCount; row++)
            {
                var testName = worksheet.Cells[row, 1].Text?.Trim();
                var testCountText = worksheet.Cells[row, 2].Text?.Trim();

                var item = new StatisticsPreviewItem
                {
                    TestName = testName
                };

                if (!int.TryParse(testCountText, out int testCount))
                {
                    item.HasError = true;
                    item.ErrorMessage += "Test adedi geçersiz. ";
                }
                else
                {
                    item.TestCount = testCount;
                }

                // DB'den test eşleştirme
                var test = _context.Tests.FirstOrDefault(t => t.TestName == testName);
                if (test != null)
                    item.TestID = test.TestID;
                else
                {
                    item.HasError = true;
                    item.ErrorMessage += "Test bulunamadı. ";
                }

                previewItems.Add(item);
            }

            return previewItems;
        }

        // PDF Parse
        public async Task<List<StatisticsPreviewItem>> ParsePdfAsync(IFormFile file)
        {
            var previewItems = new List<StatisticsPreviewItem>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var reader = new PdfReader(stream);
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                string text = PdfTextExtractor.GetTextFromPage(reader, i);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    // Beklenen format: TestAdı  Adet
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var testName = parts[0];
                    var testCountText = parts[1];

                    var item = new StatisticsPreviewItem
                    {
                        TestName = testName
                    };

                    if (!int.TryParse(testCountText, out int testCount))
                    {
                        item.HasError = true;
                        item.ErrorMessage += "Test adedi geçersiz. ";
                    }
                    else
                    {
                        item.TestCount = testCount;
                    }

                    var test = _context.Tests.FirstOrDefault(t => t.TestName == testName);
                    if (test != null)
                        item.TestID = test.TestID;
                    else
                    {
                        item.HasError = true;
                        item.ErrorMessage += "Test bulunamadı. ";
                    }

                    previewItems.Add(item);
                }
            }

            return previewItems;
        }

        public async Task<List<ShipmentPreviewItem>> ParseShipmentExcelAsync(IFormFile file)
        {
            var previewItems = new List<ShipmentPreviewItem>();
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet?.Dimension == null) return previewItems;

            int rowCount = worksheet.Dimension.End.Row;

            for (int row = 2; row <= rowCount; row++)
            {
                var testName = worksheet.Cells[row, 1].Text?.Trim();
                var sentCountText = worksheet.Cells[row, 2].Text?.Trim();

                var item = new ShipmentPreviewItem
                {
                    TestName = testName
                };

                if (!int.TryParse(sentCountText, out int sentCount))
                {
                    item.HasError = true;
                    item.ErrorMessage = "Gönderim adedi geçersiz. ";
                }
                else
                {
                    item.SentCount = sentCount;
                }

                // Test eşleştirme
                var test = _context.Tests.FirstOrDefault(t => t.TestName == testName);
                if (test != null)
                    item.TestID = test.TestID;
                else
                {
                    item.HasError = true;
                    item.ErrorMessage += "Test bulunamadı. ";
                }

                previewItems.Add(item);
            }

            return previewItems;
        }

        public async Task<List<ShipmentPreviewItem>> ParseShipmentPdfAsync(IFormFile file)
        {
            var previewItems = new List<ShipmentPreviewItem>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var reader = new PdfReader(stream);
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                string text = PdfTextExtractor.GetTextFromPage(reader, i);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    // Beklenen format: TestAdı  Adet
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var testName = parts[0];
                    var sentCountText = parts[1];

                    var item = new ShipmentPreviewItem
                    {
                        TestName = testName
                    };

                    if (!int.TryParse(sentCountText, out int sentCount))
                    {
                        item.HasError = true;
                        item.ErrorMessage = "Gönderim adedi geçersiz. ";
                    }
                    else
                    {
                        item.SentCount = sentCount;
                    }

                    var test = _context.Tests.FirstOrDefault(t => t.TestName == testName);
                    if (test != null)
                        item.TestID = test.TestID;
                    else
                    {
                        item.HasError = true;
                        item.ErrorMessage += "Test bulunamadı. ";
                    }

                    previewItems.Add(item);
                }
            }

            return previewItems;
        }

    }
}
