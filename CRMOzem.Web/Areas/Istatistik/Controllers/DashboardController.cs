using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
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

        // ---------------------------
        // EXCEL ŞABLONLARI
        // wwwroot/templates klasörüne koy:
        // - IstatistikSablon.xlsx
        // - GonderimSablon.xlsx
        // ---------------------------

        [HttpGet]
        public IActionResult DownloadUsageTemplate()
        {
            const string fileName = "IstatistikSablon.xlsx"; 
            var path = Path.Combine(_env.WebRootPath, "templates", fileName);

            if (!System.IO.File.Exists(path))
                return NotFound($"Şablon bulunamadı: {fileName}");

            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return PhysicalFile(path, contentType, fileName);
        }

        [HttpGet]
        public IActionResult DownloadShipmentTemplate()
        {
            const string fileName = "GonderimSablon.xlsx";
            var path = Path.Combine(_env.WebRootPath, "templates", fileName);

            if (!System.IO.File.Exists(path))
                return NotFound($"Şablon bulunamadı: {fileName}");

            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return PhysicalFile(path, contentType, fileName);
        }
    }
}