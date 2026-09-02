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
    [Authorize(Roles = "Yönetici,İstatistik,Lojistik,Aplikasyon")]
    public class ShipmentController : Controller
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IShipmentTemplateService _shipmentTemplateService;
        private readonly ApplicationDbContext _context;

        public ShipmentController(
            IStatisticsService statisticsService,
            IShipmentTemplateService shipmentTemplateService,
            ApplicationDbContext context)
        {
            _statisticsService = statisticsService;
            _shipmentTemplateService = shipmentTemplateService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
        {
            try
            {
                var shipments = await _statisticsService.GetShipmentsAsync(hospitalId ?? 0, startDate, endDate, ct);
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName");
                ViewBag.SelectedHospitalID = hospitalId;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                return View(shipments);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return View(new List<MonthlyShipment>());
            }
        }

        // ═════════════════════════════════════════════
        // YENİ — Şablona bağlı toplu gönderim
        // ═════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Create(int? hospitalId, DateTime? month, CancellationToken ct)
        {
            try
            {
                await LoadHospitalsAsync(hospitalId, ct);

                if (hospitalId == null || hospitalId <= 0)
                {
                    ViewBag.SelectedMonth = (month ?? DateTime.Now).ToString("yyyy-MM");
                    return View(model: null);
                }

                var m = month ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var vm = await _shipmentTemplateService.BuildFormAsync(hospitalId.Value, m, ct);

                ViewBag.SelectedMonth = m.ToString("yyyy-MM");
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Form yüklenemedi: " + ex.Message;
                await LoadHospitalsAsync(hospitalId, ct);
                return View(model: null);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShipmentTemplateSaveVm model, CancellationToken ct)
        {
            try
            {
                if (model == null || model.HospitalID <= 0)
                {
                    TempData["Error"] = "Geçersiz veri.";
                    return RedirectToAction(nameof(Create));
                }

                var result = await _shipmentTemplateService.SaveAsync(model, ct);

                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage ?? "Kayıt başarısız.";
                    return RedirectToAction(nameof(Create), new
                    {
                        hospitalId = model.HospitalID,
                        month = model.Month.ToString("yyyy-MM-dd")
                    });
                }

                var mesaj = $"İşlem başarılı: " +
                    $"{result.EklenenSatir} yeni, {result.GuncellenenSatir} güncellendi" +
                    (result.AtlananSatir > 0 ? $", {result.AtlananSatir} atlandı" : "") +
                    $". Toplam {result.ToplamAdet} adet kayıt.";

                TempData["SuccessMessage"] = mesaj;
                return RedirectToAction(nameof(Index), new { hospitalId = model.HospitalID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Create), new
                {
                    hospitalId = model?.HospitalID,
                    month = model?.Month.ToString("yyyy-MM-dd")
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            try
            {
                var shipment = await _context.MonthlyShipments.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

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

                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName", shipment.HospitalID);
                ViewBag.Tests = new SelectList(
                    await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct),
                    "TestID", "TestName", shipment.TestID);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShipmentViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(
                    await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct),
                    "TestID", "TestName", model.TestID);
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
                ModelState.AddModelError("", "Güncelleme hatası: " + ex.Message);
                ViewBag.Hospitals = new SelectList(
                    await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct),
                    "HospitalID", "HospitalName", model.HospitalID);
                ViewBag.Tests = new SelectList(
                    await _context.Tests.AsNoTracking().OrderBy(x => x.TestName).ToListAsync(ct),
                    "TestID", "TestName", model.TestID);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
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
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Balance(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
        {
            try
            {
                var balance = await _statisticsService.GetHospitalTestBalanceAsync(hospitalId, startDate, endDate, ct);
                ViewBag.Hospital = await _context.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalID == hospitalId, ct);
                return View(balance);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // ═════════════════════════════════════════════
        // Helper
        // ═════════════════════════════════════════════
        private async Task LoadHospitalsAsync(int? selectedHospitalId, CancellationToken ct)
        {
            ViewBag.Hospitals = await _context.Hospitals.AsNoTracking()
                .OrderBy(x => x.HospitalName)
                .Select(x => new SelectListItem
                {
                    Value = x.HospitalID.ToString(),
                    Text = x.HospitalName,
                    Selected = selectedHospitalId.HasValue && x.HospitalID == selectedHospitalId.Value
                })
                .ToListAsync(ct);
        }
    }
}