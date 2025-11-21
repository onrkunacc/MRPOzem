using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "İstatistik,Yönetici")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDashboardService _service;

        public DashboardController(ApplicationDbContext context, IDashboardService service)
        {
            _context = context;
            _service = service;
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
    }
}