using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels.KotaTakip;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Finans.Controllers
{
    [Area("Finans")]
    [Authorize(Roles = "Finans,Muhasebe,Yönetici")]
    public class KotaTakipController : Controller
    {
        private readonly IKotaTakipService _service;
        private readonly UserManager<ApplicationUser> _userManager;

        public KotaTakipController(IKotaTakipService service, UserManager<ApplicationUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? yil, int? ay, int? editId)
        {
            var secilenYil = yil ?? DateTime.Now.Year;

            try
            {
                return View(await _service.GetIndexAsync(secilenYil, ay, editId));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Warning"] = ex.Message;
                return View(await _service.GetIndexAsync(secilenYil, ay));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KayitKaydet(
            [Bind(Prefix = "Kayit")] KotaFinansKaydiInputViewModel model,
            int? filtreAy)
        {
            if (!ModelState.IsValid)
                return await VeriGirisiniTekrarGoster(model, filtreAy);

            try
            {
                await _service.KayitKaydetAsync(model, _userManager.GetUserId(User));
                TempData["SuccessMessage"] = model.Id > 0
                    ? "Finans kaydı güncellendi."
                    : "Finans kaydı eklendi.";
                return RedirectToAction(nameof(Index), new { yil = model.DonemYil, ay = filtreAy });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return await VeriGirisiniTekrarGoster(model, filtreAy);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KayitSil(int id, int yil, int? ay)
        {
            try
            {
                await _service.KayitSilAsync(id);
                TempData["SuccessMessage"] = "Finans kaydı silindi.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index), new { yil, ay });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KotaEkle(
            [Bind(Prefix = "YeniKota")] KotaInputViewModel model,
            int yil,
            int? ay)
        {
            if (!ModelState.IsValid)
            {
                var sayfa = await _service.GetIndexAsync(yil, ay);
                sayfa.YeniKota = model;
                return View("Index", sayfa);
            }

            try
            {
                await _service.KotaEkleAsync(model);
                TempData["SuccessMessage"] = "Yeni kota eklendi; seçim listelerinde hemen kullanılabilir.";
                return RedirectToAction(nameof(Index), new { yil, ay });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var sayfa = await _service.GetIndexAsync(yil, ay);
                sayfa.YeniKota = model;
                return View("Index", sayfa);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KotaCariEkle(
            [Bind(Prefix = "YeniKotaCari")] KotaCariInputViewModel model,
            int yil,
            int? ay)
        {
            if (!ModelState.IsValid)
            {
                var sayfa = await _service.GetIndexAsync(yil, ay);
                sayfa.YeniKotaCari = model;
                return View("Index", sayfa);
            }

            try
            {
                await _service.KotaCariEkleAsync(model);
                TempData["SuccessMessage"] = "Cari kota ile eşleştirildi.";
                return RedirectToAction(nameof(Index), new { yil, ay });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var sayfa = await _service.GetIndexAsync(yil, ay);
                sayfa.YeniKotaCari = model;
                return View("Index", sayfa);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KotaCariSil(int id, int yil, int? ay)
        {
            await _service.KotaCariSilAsync(id);
            TempData["SuccessMessage"] = "Cari-kota eşleştirmesi kaldırıldı.";
            return RedirectToAction(nameof(Index), new { yil, ay });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GelirleriSenkronizeEt(int yil, int? ay)
        {
            var sonuc = await _service.GelirleriSenkronizeEtAsync(yil, ay, _userManager.GetUserId(User));

            TempData["SuccessMessage"] = $"Gelir aktarımı tamamlandı: {sonuc.Eklenen} yeni, {sonuc.Guncellenen} güncel, {sonuc.Kaldirilan} geçersiz eşleşme kaldırıldı.";
            if (sonuc.Eslestirilemeyen > 0)
                TempData["Warning"] = $"{sonuc.Eslestirilemeyen} fatura için geçerli kota-cari eşleştirmesi bulunamadı.";

            return RedirectToAction(nameof(Index), new { yil, ay });
        }

        [HttpGet]
        public async Task<IActionResult> Ozet(int? yil, int? ay)
        {
            return View(await _service.GetOzetAsync(yil ?? DateTime.Now.Year, ay));
        }

        private async Task<IActionResult> VeriGirisiniTekrarGoster(KotaFinansKaydiInputViewModel model, int? filtreAy)
        {
            var sayfa = await _service.GetIndexAsync(model.DonemYil, filtreAy);
            sayfa.Kayit = model;
            return View("Index", sayfa);
        }
    }
}
