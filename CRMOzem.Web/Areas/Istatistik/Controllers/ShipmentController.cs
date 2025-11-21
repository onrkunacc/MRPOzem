using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik")]
    public class ShipmentController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public ShipmentController(IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
        {
            try
            {
                var shipments = await _statisticsService.GetShipmentsAsync(hospitalId ?? 0, startDate, endDate, ct);
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
                ViewBag.SelectedHospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                return View(shipments);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            try
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName");
                return View(new ShipmentViewModel { Month = DateTime.Now });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShipmentViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName");
                return View(model);
            }

            try
            {
                await _statisticsService.AddShipmentAsync(model.HospitalID, model.TestID, model.Month, model.SentCount, ct);
                TempData["SuccessMessage"] = "Gönderim başarıyla kaydedildi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            try
            {
                var shipment = await _context.MonthlyShipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                if (shipment == null)
                {
                    TempData["Error"] = "Gönderim bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                var model = new ShipmentViewModel
                {
                    Id = shipment.Id,
                    HospitalID = shipment.HospitalID,
                    TestID = shipment.TestID,
                    Month = shipment.Month,
                    SentCount = shipment.SentCount
                };

                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", shipment.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", shipment.TestID);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShipmentViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", model.TestID);
                return View(model);
            }

            try
            {
                var shipment = await _context.MonthlyShipments.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (shipment == null)
                {
                    TempData["Error"] = "Gönderim bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // shipment.Id = model.Id; // GEREKSİZ — kaldırıldı
                shipment.HospitalID = model.HospitalID;
                shipment.TestID = model.TestID;
                shipment.Month = new DateTime(model.Month.Year, model.Month.Month, 1);
                shipment.SentCount = model.SentCount;

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Gönderim başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                // Unique index çakışması vb.
                ModelState.AddModelError("", $"Güncelleme hatası: {ex.Message}");
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct), "TestID", "TestName", model.TestID);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                var shipment = await _context.MonthlyShipments.FindAsync(new object?[] { id }, ct);
                if (shipment == null)
                {
                    TempData["Error"] = "Gönderim bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                _context.MonthlyShipments.Remove(shipment);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Gönderim başarıyla silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Balance(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
        {
            try
            {
                var balance = await _statisticsService.GetHospitalTestBalanceAsync(hospitalId, startDate, endDate, ct);
                ViewBag.Hospital = await _context.Hospitals.AsNoTracking().FirstOrDefaultAsync(h => h.HospitalID == hospitalId, ct);
                return View(balance);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
