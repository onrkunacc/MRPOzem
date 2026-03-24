using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EkGiderModel = CRMOzem.Core.Models.EkGider;
using IhaleKalemModel = CRMOzem.Core.Models.IhaleKalem;
using IhaleModel = CRMOzem.Core.Models.Ihale;
using PersonelGiderModel = CRMOzem.Core.Models.PersonelGider;

namespace CRMOzem.Web.Areas.Ihale.Controllers
{
    [Area("Ihale")]
    [Authorize(Roles = "İhale,Yönetici")]
    public class IhaleController : Controller
    {
        private readonly IIhaleService _svc;
        public IhaleController(IIhaleService svc) => _svc = svc;

        public async Task<IActionResult> Index()
        {
            var list = await _svc.GetAllAsync();
            return View(list);
        }

        public IActionResult Create() => View(new IhaleModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IhaleModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var id = await _svc.CreateAsync(model);
            TempData["Success"] = "İhale başarıyla oluşturuldu.";
            return RedirectToAction("Detail", new { id });
        }

        public async Task<IActionResult> Detail(int id)
        {
            var ihale = await _svc.GetByIdAsync(id);
            if (ihale == null) return NotFound();
            ViewBag.Birimler = await _svc.GetBirimlerAsync();
            return View(ihale);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var ihale = await _svc.GetByIdAsync(id);
            if (ihale == null) return NotFound();
            return View(ihale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(IhaleModel model)
        {
            if (!ModelState.IsValid) return View(model);
            await _svc.UpdateAsync(model);
            TempData["Success"] = "İhale güncellendi.";
            return RedirectToAction("Detail", new { id = model.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id);
            TempData["Success"] = "İhale silindi.";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Ozet(int id)
        {
            var ozet = await _svc.GetOzetAsync(id);
            return View(ozet);
        }

        [HttpPost]
        public async Task<IActionResult> PersonelEkle([FromBody] PersonelGiderModel model)
        {
            var saved = await _svc.SavePersonelAsync(model);
            return Json(new { success = true, id = saved.Id, toplamTRY = saved.ToplamTRY });
        }

        [HttpPost]
        public async Task<IActionResult> PersonelSil(int id)
        {
            await _svc.DeletePersonelAsync(id);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> EkGiderEkle([FromBody] EkGiderModel model)
        {
            var saved = await _svc.SaveEkGiderAsync(model);
            return Json(new { success = true, id = saved.Id, tutarTRY = saved.TutarTRY });
        }

        [HttpPost]
        public async Task<IActionResult> EkGiderSil(int id)
        {
            await _svc.DeleteEkGiderAsync(id);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> KalemEkle([FromBody] IhaleKalemModel model)
        {
            var saved = await _svc.SaveKalemAsync(model);
            return Json(new { success = true, id = saved.Id, toplamSUT = saved.ToplamSUT, toplamSutFiyat = saved.ToplamSutFiyat });
        }

        [HttpPost]
        public async Task<IActionResult> KalemSil(int id)
        {
            await _svc.DeleteKalemAsync(id);
            return Json(new { success = true });
        }
    }
}