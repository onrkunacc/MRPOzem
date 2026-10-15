using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Finans.Controllers
{
    [Area("Finans")]
    [Authorize(Roles = "Yönetici,KotaTakip")]
    public class TedarikciController : Controller
    {
        private readonly ITedarikciService _service;

        public TedarikciController(ITedarikciService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var list = await _service.GetAllAsync(false, ct);
            return View(list);
        }

        [HttpGet]
        public IActionResult Create() => View(new TedarikciCreateVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TedarikciCreateVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                await _service.CreateAsync(vm, ct);
                TempData["SuccessMessage"] = "Tedarikçi eklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var vm = await _service.GetByIdAsync(id, ct);
            if (vm == null)
            {
                TempData["Error"] = "Tedarikçi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TedarikciEditVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                await _service.UpdateAsync(vm, ct);
                TempData["SuccessMessage"] = "Tedarikçi güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                await _service.DeleteAsync(id, ct);
                TempData["SuccessMessage"] = "Tedarikçi silindi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Silme hatası: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
        {
            try
            {
                await _service.ToggleActiveAsync(id, ct);
                TempData["SuccessMessage"] = "Durum güncellendi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}