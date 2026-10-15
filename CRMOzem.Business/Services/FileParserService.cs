using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class FileParserService : IFileParserService
    {
        private readonly ApplicationDbContext _context;

        public FileParserService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════════════════
        // Yardımcı: Test lookup önbelleği (SUT-first, ad-fallback)
        // ═══════════════════════════════════════════════════════════
        private class TestLookupCache
        {
            public Dictionary<string, (int TestID, string TestName, string? SutCode)> BySut { get; }
            public Dictionary<string, (int TestID, string TestName, string? SutCode)> ByName { get; }

            public TestLookupCache(
                Dictionary<string, (int, string, string?)> bySut,
                Dictionary<string, (int, string, string?)> byName)
            {
                BySut = bySut;
                ByName = byName;
            }
        }

        private async Task<TestLookupCache> LoadTestLookupAsync(CancellationToken ct = default)
        {
            var tests = await _context.Tests.AsNoTracking()
                .Select(t => new { t.TestID, t.TestName, t.SutCode })
                .ToListAsync(ct);

            var bySut = new Dictionary<string, (int, string, string?)>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, (int, string, string?)>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in tests)
            {
                if (!string.IsNullOrWhiteSpace(t.SutCode))
                {
                    var key = t.SutCode.Trim();
                    if (!bySut.ContainsKey(key))
                        bySut[key] = (t.TestID, t.TestName, t.SutCode);
                }

                if (!string.IsNullOrWhiteSpace(t.TestName))
                {
                    var key = NormalizeName(t.TestName);
                    if (!byName.ContainsKey(key))
                        byName[key] = (t.TestID, t.TestName, t.SutCode);
                }
            }

            return new TestLookupCache(bySut, byName);
        }

        private static string NormalizeName(string name)
            => (name ?? "").Trim().ToLowerInvariant();

        /// <summary>SUT-first, isim-fallback eşleştirme</summary>
        private static (int? TestID, string? SutCode, string TestName, string MatchSource, string? Error)
            MatchTest(TestLookupCache cache, string? sutCode, string? testName)
        {
            var displayName = (testName ?? "").Trim();
            var sut = (sutCode ?? "").Trim();

            // 1) Önce SUT koduyla dene
            if (!string.IsNullOrWhiteSpace(sut) && cache.BySut.TryGetValue(sut, out var bySut))
            {
                return (bySut.TestID, bySut.SutCode, bySut.TestName, "sut", null);
            }

            // 2) Fallback: test adıyla dene
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var nameKey = NormalizeName(displayName);
                if (cache.ByName.TryGetValue(nameKey, out var byName))
                {
                    var err = string.IsNullOrWhiteSpace(sut)
                        ? null
                        : $"SUT '{sut}' bulunamadı, isimle eşleşti.";
                    return (byName.TestID, byName.SutCode, byName.TestName, "name", err);
                }
            }

            // 3) Bulunamadı
            var errMsg = string.IsNullOrWhiteSpace(sut)
                ? $"Test bulunamadı: '{displayName}'"
                : $"Test bulunamadı: SUT='{sut}', Ad='{displayName}'";
            return (null, null, displayName, "none", errMsg);
        }

        // ═══════════════════════════════════════════════════════════
        // STATİSTİK (Kullanım) — Excel
        // Beklenen kolonlar: [1]SUT Kodu, [2]Test Adı, [3]Tip (opsiyonel), [4]Miktar
        // ═══════════════════════════════════════════════════════════
        public async Task<List<StatisticsPreviewItem>> ParseExcelAsync(IFormFile file)
        {
            var previewItems = new List<StatisticsPreviewItem>();
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            var cache = await LoadTestLookupAsync();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet?.Dimension == null) return previewItems;

            int rowCount = worksheet.Dimension.End.Row;
            int colCount = worksheet.Dimension.End.Column;

            // Başlık satırından kolon indekslerini tespit et
            var (headerRow, sutCol, nameCol, countCol) = DetectColumns(worksheet, colCount, isShipment: false);

            for (int row = headerRow + 1; row <= rowCount; row++)
            {
                var sutCode = sutCol > 0 ? worksheet.Cells[row, sutCol].Text?.Trim() : null;
                var testName = nameCol > 0 ? worksheet.Cells[row, nameCol].Text?.Trim() : null;
                var countText = countCol > 0 ? worksheet.Cells[row, countCol].Text?.Trim() : null;

                // Tamamen boş satırları atla
                if (string.IsNullOrWhiteSpace(sutCode) &&
                    string.IsNullOrWhiteSpace(testName) &&
                    string.IsNullOrWhiteSpace(countText))
                    continue;

                var (testId, matchedSut, matchedName, source, matchErr) = MatchTest(cache, sutCode, testName);

                var item = new StatisticsPreviewItem
                {
                    TestID = testId,
                    SutCode = matchedSut ?? sutCode,
                    TestName = matchedName,
                    MatchSource = source
                };

                if (matchErr != null)
                {
                    item.HasError = testId == null;
                    item.ErrorMessage = matchErr + " ";
                }

                // Miktar boş veya 0 ise atla (kullanıcı doldurmamış)
                if (string.IsNullOrWhiteSpace(countText))
                    continue;

                if (!int.TryParse(countText, NumberStyles.Any, CultureInfo.InvariantCulture, out int testCount) &&
                    !int.TryParse(countText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out testCount))
                {
                    item.HasError = true;
                    item.ErrorMessage += "Miktar geçersiz. ";
                }
                else if (testCount < 0)
                {
                    item.HasError = true;
                    item.ErrorMessage += "Miktar negatif olamaz. ";
                }
                else
                {
                    item.TestCount = testCount;
                }

                previewItems.Add(item);
            }

            return previewItems;
        }

        // ═══════════════════════════════════════════════════════════
        // STATİSTİK — PDF (SUT-first)
        // Format: her satırda "SUT_KODU  TEST_ADI...  MIKTAR"
        // ═══════════════════════════════════════════════════════════
        public async Task<List<StatisticsPreviewItem>> ParsePdfAsync(IFormFile file)
        {
            var previewItems = new List<StatisticsPreviewItem>();
            var cache = await LoadTestLookupAsync();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var reader = new PdfReader(stream);
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                string text = PdfTextExtractor.GetTextFromPage(reader, i);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    // İlk token SUT, son token miktar, arası test adı
                    var sutCode = parts[0];
                    var countText = parts[^1];
                    var testName = parts.Length > 2
                        ? string.Join(" ", parts.Skip(1).Take(parts.Length - 2))
                        : "";

                    var (testId, matchedSut, matchedName, source, matchErr) = MatchTest(cache, sutCode, testName);

                    var item = new StatisticsPreviewItem
                    {
                        TestID = testId,
                        SutCode = matchedSut ?? sutCode,
                        TestName = matchedName,
                        MatchSource = source
                    };

                    if (matchErr != null)
                    {
                        item.HasError = testId == null;
                        item.ErrorMessage = matchErr + " ";
                    }

                    if (!int.TryParse(countText, out int testCount))
                    {
                        item.HasError = true;
                        item.ErrorMessage += "Miktar geçersiz. ";
                    }
                    else
                    {
                        item.TestCount = testCount;
                    }

                    previewItems.Add(item);
                }
            }

            return previewItems;
        }

        // ═══════════════════════════════════════════════════════════
        // GÖNDERİM — Excel
        // ═══════════════════════════════════════════════════════════
        public async Task<List<ShipmentPreviewItem>> ParseShipmentExcelAsync(IFormFile file)
        {
            var previewItems = new List<ShipmentPreviewItem>();
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            var cache = await LoadTestLookupAsync();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet?.Dimension == null) return previewItems;

            int rowCount = worksheet.Dimension.End.Row;
            int colCount = worksheet.Dimension.End.Column;

            var (headerRow, sutCol, nameCol, countCol) = DetectColumns(worksheet, colCount, isShipment: true);

            for (int row = headerRow + 1; row <= rowCount; row++)
            {
                var sutCode = sutCol > 0 ? worksheet.Cells[row, sutCol].Text?.Trim() : null;
                var testName = nameCol > 0 ? worksheet.Cells[row, nameCol].Text?.Trim() : null;
                var countText = countCol > 0 ? worksheet.Cells[row, countCol].Text?.Trim() : null;

                if (string.IsNullOrWhiteSpace(sutCode) &&
                    string.IsNullOrWhiteSpace(testName) &&
                    string.IsNullOrWhiteSpace(countText))
                    continue;

                var (testId, matchedSut, matchedName, source, matchErr) = MatchTest(cache, sutCode, testName);

                var item = new ShipmentPreviewItem
                {
                    TestID = testId,
                    SutCode = matchedSut ?? sutCode,
                    TestName = matchedName,
                    MatchSource = source
                };

                if (matchErr != null)
                {
                    item.HasError = testId == null;
                    item.ErrorMessage = matchErr + " ";
                }

                if (string.IsNullOrWhiteSpace(countText))
                    continue;

                if (!int.TryParse(countText, NumberStyles.Any, CultureInfo.InvariantCulture, out int sentCount) &&
                    !int.TryParse(countText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out sentCount))
                {
                    item.HasError = true;
                    item.ErrorMessage += "Gönderim miktarı geçersiz. ";
                }
                else if (sentCount < 0)
                {
                    item.HasError = true;
                    item.ErrorMessage += "Miktar negatif olamaz. ";
                }
                else
                {
                    item.SentCount = sentCount;
                }

                previewItems.Add(item);
            }

            return previewItems;
        }

        // ═══════════════════════════════════════════════════════════
        // GÖNDERİM — PDF
        // ═══════════════════════════════════════════════════════════
        public async Task<List<ShipmentPreviewItem>> ParseShipmentPdfAsync(IFormFile file)
        {
            var previewItems = new List<ShipmentPreviewItem>();
            var cache = await LoadTestLookupAsync();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var reader = new PdfReader(stream);
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                string text = PdfTextExtractor.GetTextFromPage(reader, i);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var sutCode = parts[0];
                    var countText = parts[^1];
                    var testName = parts.Length > 2
                        ? string.Join(" ", parts.Skip(1).Take(parts.Length - 2))
                        : "";

                    var (testId, matchedSut, matchedName, source, matchErr) = MatchTest(cache, sutCode, testName);

                    var item = new ShipmentPreviewItem
                    {
                        TestID = testId,
                        SutCode = matchedSut ?? sutCode,
                        TestName = matchedName,
                        MatchSource = source
                    };

                    if (matchErr != null)
                    {
                        item.HasError = testId == null;
                        item.ErrorMessage = matchErr + " ";
                    }

                    if (!int.TryParse(countText, out int sentCount))
                    {
                        item.HasError = true;
                        item.ErrorMessage += "Miktar geçersiz. ";
                    }
                    else
                    {
                        item.SentCount = sentCount;
                    }

                    previewItems.Add(item);
                }
            }

            return previewItems;
        }

        // ═══════════════════════════════════════════════════════════
        // Kolon başlıklarını tespit et (esnek, farklı şablon versiyonları için)
        // ═══════════════════════════════════════════════════════════
        private static (int headerRow, int sutCol, int nameCol, int countCol) DetectColumns(
      ExcelWorksheet ws, int colCount, bool isShipment)
        {
            int rowCount = ws.Dimension.End.Row;
            int maxSearchRow = Math.Min(10, rowCount);  // ilk 10 satırda başlık ara

            // 1'den 10'a kadar her satırı başlık olarak dene
            for (int r = 1; r <= maxSearchRow; r++)
            {
                int sutCol = 0, nameCol = 0, countCol = 0;

                for (int c = 1; c <= colCount; c++)
                {
                    var header = (ws.Cells[r, c].Text ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(header)) continue;

                    if (sutCol == 0 && (header.Contains("sut") || header.Contains("kod") || header.Contains("referans")))
                        sutCol = c;
                    else if (nameCol == 0 && header.Contains("test") && header.Contains("ad"))
                        nameCol = c;
                    else if (nameCol == 0 && header == "test")
                        nameCol = c;
                    else if (countCol == 0 && (header.Contains("miktar") || header.Contains("adet") ||
                        header.Contains("say") || header.Contains("kullan") || header.Contains("gönder") ||
                        header.Contains("gonder")))
                        countCol = c;
                }

                // En az miktar + (SUT veya isim) kolonu bulunduysa geçerli başlık satırıdır
                if (countCol > 0 && (sutCol > 0 || nameCol > 0))
                {
                    return (r, sutCol, nameCol, countCol);
                }
            }

            // Hiç başlık bulunamadıysa — eski format varsayımı: satır 1 başlık, 2'den veri
            return (1, 0, 1, 2);
        }
    }
}