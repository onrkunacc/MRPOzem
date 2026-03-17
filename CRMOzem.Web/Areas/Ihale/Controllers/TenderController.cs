using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM0zem.Web.Areas.Ihale.Controllers
{
    [Area("Ihale")]
    [Authorize(Roles = "Yönetici,İhale")]
    public class TenderController : Controller
    {
        private readonly ITenderService _tenderService;
        private readonly ITenderCalculatorService _calculator;
        private readonly ITenderExportService _export;

        public TenderController(
            ITenderService tenderService,
            ITenderCalculatorService calculator,
            ITenderExportService export)
        {
            _tenderService = tenderService;
            _calculator = calculator;
            _export = export;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var tenders = await _tenderService.GetAllAsync();
                foreach (var t in tenders) _calculator.Calculate(t);
                return View(tenders);
            }
            catch
            {
                return View(new List<Tender>());
            }
        }

        public IActionResult Create()
        {
            try
            {
                var vm = new TenderViewModel
                {
                    Tender = new Tender { TenderDate = DateTime.Now, DurationMonths = 0 }
                };
                return PartialView("_TenderCreateEditModal", vm);
            }
            catch
            {
                return BadRequest("Form yüklenemedi.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TenderViewModel model)
        {
            try
            {
                // nav doğrulama temizliği (savunmacı)
                ModelState.Remove("Tender.Tests[].Tender");
                ModelState.Remove("Tender.ExtraCosts[].Tender");
                ModelState.Remove("Tender.Personnels[].Tender");

                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Doğrulama hatası", errors = ModelStateToDict() });

                // Create sırasında Id = 0 olmalı
                model.Tender.Id = 0;

                await _tenderService.CreateAsync(model.Tender);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var tender = await _tenderService.GetByIdAsync(id);
                if (tender == null) return NotFound();

                _calculator.Calculate(tender);

                var vm = new TenderViewModel
                {
                    Tender = tender,
                    Tests = tender.Tests?.ToList() ?? new(),
                    ExtraCosts = tender.ExtraCosts?.ToList() ?? new(),
                    Personnels = tender.Personnels?.ToList() ?? new()
                };

                return PartialView("_TenderCreateEditModal", vm);
            }
            catch
            {
                return BadRequest("Form yüklenemedi.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TenderViewModel model)
        {
            try
            {
                ModelState.Remove("Tender.Tests[].Tender");
                ModelState.Remove("Tender.ExtraCosts[].Tender");
                ModelState.Remove("Tender.Personnels[].Tender");

                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Doğrulama hatası", errors = ModelStateToDict() });

                // kritik: Id 0 geldiyse yanlış post var
                if (model.Tender.Id <= 0)
                    return BadRequest(new { success = false, message = "Geçersiz ihale id. Formda hidden Id alanını kontrol edin." });

                await _tenderService.UpdateAsync(model.Tender);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var tender = await _tenderService.GetByIdAsync(id);
                if (tender == null) return NotFound();

                _calculator.Calculate(tender);

                var vm = new TenderViewModel
                {
                    Tender = tender,
                    Tests = tender.Tests?.ToList() ?? new(),
                    ExtraCosts = tender.ExtraCosts?.ToList() ?? new(),
                    Personnels = tender.Personnels?.ToList() ?? new(),
                    TotalTestCost = tender.TotalTestCost,
                    TotalExtraCost = tender.TotalExtraCost,
                    TotalPersonnelCost = tender.TotalPersonnelCost,
                    OverallCost = tender.OverallCost
                };

                return PartialView("_TenderDetailsModal", vm);
            }
            catch
            {
                return BadRequest("Detaylar alınamadı.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _tenderService.DeleteAsync(id);
                return Json(new { success = result });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        // --- EXPORTS ---
        [HttpGet]
        public async Task<IActionResult> ExportExcel(DateTime date)
        {
            var bytes = await _export.ExportExcelDailyAsync(date.Date);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"IhaleRapor_{date:yyyyMMdd}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(DateTime date)
        {
            var bytes = await _export.ExportPdfDailyAsync(date.Date);
            return File(bytes, "application/pdf", $"IhaleRapor_{date:yyyyMMdd}.pdf");
        }

        private Dictionary<string, string[]> ModelStateToDict()
            => ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                         .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
    }
}