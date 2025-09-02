
using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.AccessControl;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize("Yönetici,İstatistik")]
    public class ShipmentController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public ShipmentController(IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _context = context;
        }
        // Gönderim Listesi
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

        // Yeni Gönderim Formu
        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
                ViewBag.Tests = new SelectList(_context.Tests, "TestID", "TestName");
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Yeni Gönderim Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int hospitalId, int testId, DateTime month, int sentCount)
        {
            try
            {
                if (sentCount < 0)
                {
                    TempData["Error"] = "Gönderim adedi negatif olamaz.";
                    return RedirectToAction("Create");
                }

                await _statisticsService.AddShipmentAsync(hospitalId, testId, month, sentCount);

                TempData["SuccessMessage"] = "Gönderim başarıyla kaydedildi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
