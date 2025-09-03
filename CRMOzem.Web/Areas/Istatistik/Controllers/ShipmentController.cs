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

        // Listeleme
        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var shipments = await _statisticsService.GetShipmentsAsync(hospitalId ?? 0, startDate, endDate);

                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
                ViewBag.SelectedHospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View(shipments);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Yeni Gönderim
        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
                ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName");
                return View(new ShipmentViewModel { Month = DateTime.Now });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShipmentViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
                    ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName");
                    return View(model);
                }

                await _statisticsService.AddShipmentAsync(model.HospitalID, model.TestID, model.Month, model.SentCount);
                TempData["SuccessMessage"] = "Gönderim başarıyla kaydedildi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Gönderim Düzenleme
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var shipment = await _context.MonthlyShipments.FindAsync(id);
                if (shipment == null)
                {
                    TempData["Error"] = "Gönderim bulunamadı.";
                    return RedirectToAction("Index");
                }

                var model = new ShipmentViewModel
                {
                    HospitalID = shipment.HospitalID,
                    TestID = shipment.TestID,
                    Month = shipment.Month,
                    SentCount = shipment.SentCount
                };

                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", shipment.HospitalID);
                ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName", shipment.TestID);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShipmentViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                    ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName", model.TestID);
                    return View(model);
                }

                var shipment = await _context.MonthlyShipments.FindAsync(id);
                if (shipment == null)
                {
                    TempData["Error"] = "Gönderim bulunamadı.";
                    return RedirectToAction("Index");
                }

                shipment.HospitalID = model.HospitalID;
                shipment.TestID = model.TestID;
                shipment.Month = model.Month;
                shipment.SentCount = model.SentCount;

                _context.MonthlyShipments.Update(shipment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Gönderim başarıyla güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Gönderim Silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var shipment = await _context.MonthlyShipments.FindAsync(id);
                if (shipment == null)
                {
                    TempData["Error"] = "Gönderim bulunamadı.";
                    return RedirectToAction("Index");
                }

                _context.MonthlyShipments.Remove(shipment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Gönderim başarıyla silindi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Kalan raporu
        [HttpGet]
        public async Task<IActionResult> Balance(int hospitalId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var balance = await _statisticsService.GetHospitalTestBalanceAsync(hospitalId, startDate, endDate);
                ViewBag.Hospital = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == hospitalId);
                return View(balance);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
