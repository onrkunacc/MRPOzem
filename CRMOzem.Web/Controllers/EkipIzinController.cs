using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
  
    [Authorize]
    public class EkipIzinController : Controller
    {
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public EkipIzinController(
            ILeaveService leaveService,
            UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? yil)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var secilenYil = yil ?? DateTime.Now.Year;
            var model = await _leaveService.GetEkipTalepleriAsync(userId, secilenYil);

            return View(model);
        }

        /// <summary>Karar verirken çalışanın bakiyesi ve ekip çakışmaları.</summary>
        [HttpGet]
        public async Task<IActionResult> Detay(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var baglam = await _leaveService.GetOnayBaglamiAsync(id);
            if (baglam?.Talep == null)
            {
                TempData["Error"] = "İzin talebi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Sadece kendi ekibinin talebini görebilir
            var astlar = await _leaveService.GetAstIdleriAsync(userId);
            if (!astlar.Contains(baglam.Talep.UserId))
            {
                TempData["Error"] = "Bu talep sizin ekibinizde değil.";
                return RedirectToAction(nameof(Index));
            }

            return View(baglam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id, string? not, string? donus)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var (basarili, hata) = await _leaveService.SupervisorApproveAsync(id, userId, not);

            if (basarili)
                TempData["Success"] = "Onayladınız. Talep İnsan Kaynakları onayına geçti.";
            else
                TempData["Error"] = hata ?? "Onay işlemi başarısız.";

            return donus == "detay"
                ? RedirectToAction(nameof(Detay), new { id })
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string gerekce, string? donus)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var (basarili, hata) = await _leaveService.SupervisorRejectAsync(id, userId, gerekce);

            if (basarili)
                TempData["Success"] = "Talep reddedildi. Gerekçe çalışana görünecek.";
            else
                TempData["Error"] = hata ?? "Red işlemi başarısız.";

            return donus == "detay"
                ? RedirectToAction(nameof(Detay), new { id })
                : RedirectToAction(nameof(Index));
        }
    }
}