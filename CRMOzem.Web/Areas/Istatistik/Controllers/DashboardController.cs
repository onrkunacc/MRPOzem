using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "İstatistik,Yönetici")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDashboardService _service;
        private readonly IWebHostEnvironment _env; // webroot için

        public DashboardController(
            ApplicationDbContext context,
            IDashboardService service,
            IWebHostEnvironment env)
        {
            _context = context;
            _service = service;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Hospitals = await _context.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            return View();
        }

        // ====================================================================
        // VERİ GİRİŞİ DURUMU
        // Seçilen ay için gönderim (MonthlyShipment) ve kullanım (MonthlyStatistics)
        // kaydı girilmiş hastaneleri raporlar.
        // ====================================================================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetMissingData(int? year, int? month, CancellationToken ct = default)
        {
            // Varsayılan: içinde bulunduğumuz ayın ÖNCEKİ ayı
            // (Örn: Ekim'deysek Eylül kontrol edilir)
            var now = DateTime.Now;
            var targetYear = year ?? now.AddMonths(-1).Year;
            var targetMonth = month ?? now.AddMonths(-1).Month;

            // Ay aralığı [monthStart, monthEnd) — SQL Server için index-friendly
            // MonthlyShipment.Month ve MonthlyStatistics.Month DateTime olduğu için
            // DATEPART yerine range query kullanmak hem index'i korur hem sadeleştirir.
            var monthStart = new DateTime(targetYear, targetMonth, 1);
            var monthEnd = monthStart.AddMonths(1);

            // Tüm hastaneler (alfabetik)
            var hospitals = await _context.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            // O ay için gönderim kaydı girilmiş hastane ID'leri
            var shipmentHospitalIds = await _context.MonthlyShipments
                .AsNoTracking()
                .Where(s => s.Month >= monthStart && s.Month < monthEnd)
                .Select(s => s.HospitalID)
                .Distinct()
                .ToListAsync(ct);

            // O ay için kullanım (MonthlyStatistics) kaydı girilmiş hastane ID'leri
            var usageHospitalIds = await _context.MonthlyStatistics
                .AsNoTracking()
                .Where(u => u.Month >= monthStart && u.Month < monthEnd)
                .Select(u => u.HospitalID)
                .Distinct()
                .ToListAsync(ct);

            var shipmentSet = shipmentHospitalIds.ToHashSet();
            var usageSet = usageHospitalIds.ToHashSet();

            var items = hospitals.Select(h =>
            {
                var hasShip = shipmentSet.Contains(h.HospitalID);
                var hasUse = usageSet.Contains(h.HospitalID);
                var status = (hasShip && hasUse) ? "complete"
                           : (hasShip || hasUse) ? "partial"
                           : "empty";
                return new
                {
                    hospitalId = h.HospitalID,
                    hospitalName = h.HospitalName,
                    hasShipment = hasShip,
                    hasUsage = hasUse,
                    status
                };
            })
            // Önce boş, sonra yarı, en sonra tam; her grup içinde alfabetik
            .OrderBy(x => x.status == "empty" ? 0 : x.status == "partial" ? 1 : 2)
            .ThenBy(x => x.hospitalName)
            .ToList();

            var trCulture = new CultureInfo("tr-TR");

            return Json(new
            {
                year = targetYear,
                month = targetMonth,
                monthLabel = monthStart.ToString("MMMM yyyy", trCulture),
                monthShortLabel = monthStart.ToString("MMM yyyy", trCulture),
                totalHospitals = items.Count,
                completeCount = items.Count(x => x.status == "complete"),
                partialCount = items.Count(x => x.status == "partial"),
                emptyCount = items.Count(x => x.status == "empty"),
                hospitals = items
            });
        }


        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetKpis(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
            => Json(await _service.GetKpisAsync(hospitalId, startDate, endDate, ct));

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetTrends(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
            => Json(await _service.GetTrendsAsync(hospitalId, startDate, endDate, ct));

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetTopTests(int? hospitalId, DateTime? startDate, DateTime? endDate, int take = 10, CancellationToken ct = default)
        {
            var (ship, use) = await _service.GetTopTestsAsync(hospitalId, startDate, endDate, take, ct);
            return Json(new { shipments = ship, usage = use });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetByHospital(DateTime? startDate, DateTime? endDate, int take = 12, CancellationToken ct = default)
            => Json(await _service.GetByHospitalAsync(startDate, endDate, take, ct));




        [HttpGet]
        public async Task<IActionResult> DownloadUsageTemplate(int? hospitalId, DateTime? month, CancellationToken ct)
        {
            if (hospitalId == null || hospitalId <= 0)
                return BadRequest("Lütfen bir hastane seçiniz.");

            var m = month ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return await BuildTemplateAsync(hospitalId.Value, m, isShipment: false, ct);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadShipmentTemplate(int? hospitalId, DateTime? month, CancellationToken ct)
        {
            if (hospitalId == null || hospitalId <= 0)
                return BadRequest("Lütfen bir hastane seçiniz.");

            var m = month ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return await BuildTemplateAsync(hospitalId.Value, m, isShipment: true, ct);
        }

        private async Task<IActionResult> BuildTemplateAsync(int hospitalId, DateTime month, bool isShipment, CancellationToken ct)
        {
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            var hospital = await _context.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId, ct);

            if (hospital == null) return NotFound("Hastane bulunamadı.");


            var activeTests = await _context.TestTemplates.AsNoTracking()
                .Include(t => t.Test)
                .Where(t => t.HospitalID == hospitalId && t.IsActive)
                .Select(t => new
                {
                    t.Test.TestID,
                    t.Test.TestName,
                    t.Test.SutCode,
                    t.Test.Kategori,
                    t.Test.Platform,
                    t.Test.GirisTipi,
                    t.Test.KutuBasinaAdet
                })
                .OrderBy(t => t.Platform)
                .ThenBy(t => t.Kategori)
                .ThenBy(t => t.TestName)
                .ToListAsync(ct);

            var trTR = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            var monthLabel = month.ToString("MMMM yyyy", trTR);
            var moduleLabel = isShipment ? "Gönderim" : "Kullanım (İstatistik)";
            var amountLabel = isShipment ? "Gönderim Miktarı" : "Kullanılan Miktar";
            var accentColor = isShipment
                ? System.Drawing.Color.FromArgb(16, 185, 129)   // yeşil
                : System.Drawing.Color.FromArgb(99, 102, 241);  // mor

            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(isShipment ? "Gonderim" : "Kullanim");

            // ═══ BAŞLIK ═══
            ws.Cells[1, 1].Value = $"{hospital.HospitalName} — {moduleLabel} Şablonu";
            ws.Cells[1, 1, 1, 5].Merge = true;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
            ws.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(accentColor);
            ws.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            ws.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            ws.Row(1).Height = 26;

            ws.Cells[2, 1].Value = $"Ay: {monthLabel}  •  Hastane ID: {hospitalId}  •  Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}";
            ws.Cells[2, 1, 2, 5].Merge = true;
            ws.Cells[2, 1].Style.Font.Italic = true;
            ws.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);

            ws.Cells[3, 1].Value = "⚠ SUT Kodu üzerinden eşleşme yapılır. Kodu değiştirmeyin. Sadece SARI kolonu doldurun.";
            ws.Cells[3, 1, 3, 5].Merge = true;
            ws.Cells[3, 1].Style.Font.Bold = true;
            ws.Cells[3, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(180, 83, 9));
            ws.Cells[3, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[3, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(254, 243, 199));

            // ═══ TABLO BAŞLIKLARI (SATIR 5) ═══
            string[] headers = { "SUT Kodu", "Test Adı", "Kategori", "Tip", amountLabel };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[5, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(accentColor);
                cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }
            ws.Row(5).Height = 22;

            // ═══ VERİ SATIRLARI ═══
            int row = 6;
            if (activeTests.Count == 0)
            {
                ws.Cells[row, 1].Value = $"Bu hastanede aktif test bulunamadı. Önce 'Hastane Test Seti' ekranından test aktif edin.";
                ws.Cells[row, 1, row, 5].Merge = true;
                ws.Cells[row, 1].Style.Font.Italic = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            }
            else
            {
                foreach (var t in activeTests)
                {
                    ws.Cells[row, 1].Value = t.SutCode ?? "";
                    ws.Cells[row, 2].Value = t.TestName;
                    ws.Cells[row, 3].Value = t.Kategori ?? "";
                    ws.Cells[row, 4].Value = t.GirisTipi.ToString();

                    // SUT ve Test Adı kolonları KİLİTLİ (kullanıcı değiştirmesin)
                    ws.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(241, 245, 249));
                    ws.Cells[row, 1].Style.Font.Name = "Consolas";
                    ws.Cells[row, 1].Style.Font.Bold = true;

                    ws.Cells[row, 2].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(248, 250, 252));

                    // Miktar kolonu (SARI - doldurulacak)
                    ws.Cells[row, 5].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(254, 249, 195));
                    ws.Cells[row, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium,
                        System.Drawing.Color.FromArgb(234, 179, 8));

                    // Tüm satır için hafif border
                    for (int c = 1; c <= 5; c++)
                    {
                        ws.Cells[row, c].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        ws.Cells[row, c].Style.Border.Bottom.Color.SetColor(System.Drawing.Color.LightGray);
                    }

                    row++;
                }
            }

            // ═══ KOLON GENİŞLİKLERİ ═══
            ws.Column(1).Width = 15;   // SUT
            ws.Column(2).Width = 45;   // Test Adı
            ws.Column(3).Width = 22;   // Kategori
            ws.Column(4).Width = 10;   // Tip
            ws.Column(5).Width = 18;   // Miktar

            ws.View.FreezePanes(6, 1);
            ws.View.ShowGridLines = false;

            // ═══ BİLGİ NOTU (ALTA) ═══
            if (activeTests.Count > 0)
            {
                row += 2;
                ws.Cells[row, 1].Value = $"Toplam {activeTests.Count} aktif test listelendi.";
                ws.Cells[row, 1, row, 5].Merge = true;
                ws.Cells[row, 1].Style.Font.Italic = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);

                row++;
                ws.Cells[row, 1].Value = "Miktar kolonunu doldurup dosyayı 'Toplu Yükle' ekranından yükleyin. Boş bırakılan satırlar atlanır.";
                ws.Cells[row, 1, row, 5].Merge = true;
                ws.Cells[row, 1].Style.Font.Italic = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            }

            // Dosya adı
            var safeName = string.Join("_", hospital.HospitalName.Split(Path.GetInvalidFileNameChars()));
            var fileName = isShipment
                ? $"Gonderim_{safeName}_{month:yyyyMM}.xlsx"
                : $"Kullanim_{safeName}_{month:yyyyMM}.xlsx";

            var bytes = package.GetAsByteArray();
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(bytes, contentType, fileName);
        }
    }
}