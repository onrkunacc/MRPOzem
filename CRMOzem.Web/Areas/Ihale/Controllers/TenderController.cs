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

        public TenderController(ITenderService tenderService, ITenderCalculatorService calculator)
        {
            _tenderService = tenderService;
            _calculator = calculator;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var tenders = await _tenderService.GetAllAsync();
                foreach (var t in tenders) _calculator.Calculate(t); // liste görünümü güncel
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
        public async Task<IActionResult> Create(TenderViewModel model)
        {
            try
            {
                // Nav doğrulamalarını yine de temizle (savunmacı programlama)
                ModelState.Remove("Tender.Tests[].Tender");
                ModelState.Remove("Tender.ExtraCosts[].Tender");
                ModelState.Remove("Tender.Personnels[].Tender");

                if (!ModelState.IsValid)
                {
                    var errs = ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                                         .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage));
                    return BadRequest(new { success = false, message = "Doğrulama hatası", errors = errs });
                }

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
        public async Task<IActionResult> Edit(TenderViewModel model)
        {
            try
            {
                ModelState.Remove("Tender.Tests[].Tender");
                ModelState.Remove("Tender.ExtraCosts[].Tender");
                ModelState.Remove("Tender.Personnels[].Tender");

                if (!ModelState.IsValid)
                {
                    var errs = ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                                         .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage));
                    return BadRequest(new { success = false, message = "Doğrulama hatası", errors = errs });
                }

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
    }
}

