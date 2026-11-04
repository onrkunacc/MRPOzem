using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Web.Controllers
{
    [Authorize]
    public class LeaveRequestController : Controller
    {
        private readonly ILeaveService _leaveService;
        private readonly IResmiTatilService _tatilService;
        private readonly ILeaveBelgeService _belgeService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaveRequestController(
            ILeaveService leaveService,
            IResmiTatilService tatilService,
            ILeaveBelgeService belgeService,
            UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _tatilService = tatilService;
            _belgeService = belgeService;
            _userManager = userManager;
        }

        // ═══════════════ LİSTE ═══════════════

        [HttpGet]
        public async Task<IActionResult> MyLeaves(int? yil)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var secilenYil = yil ?? DateTime.Now.Year;

            var model = new LeaveListViewModel
            {
                Year = secilenYil,
                Leaves = await _leaveService.GetUserLeavesAsync(userId, secilenYil),
                Balance = await _leaveService.GetBalanceAsync(userId, secilenYil),
                AvailableYears = await _leaveService.GetAvailableYearsAsync()
            };

            return View(model);
        }

        // ═══════════════ YENİ TALEP ═══════════════

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var model = new CreateLeaveRequestViewModel();
            await DoldurAsync(model, userId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateLeaveRequestViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!ModelState.IsValid)
            {
                await DoldurAsync(model, userId);
                return View(model);
            }

            var request = new LeaveRequest
            {
                UserId = userId,
                LeaveTypeId = model.LeaveTypeId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                StartHour = model.StartHour,
                EndHour = model.EndHour,
                Reason = model.Reason,
                SaturdayExcluded = model.SaturdayExcluded
            };

            var (basarili, hata) = await _leaveService.CreateLeaveAsync(request);

            if (!basarili)
            {
                ModelState.AddModelError("", hata ?? "İzin talebi oluşturulamadı.");
                await DoldurAsync(model, userId);
                return View(model);
            }

            TempData["Success"] = $"İzin talebiniz gönderildi. {request.TotalDays} iş günü, onay bekliyor.";
            return RedirectToAction(nameof(MyLeaves));
        }

        // ═══════════════ İPTAL ═══════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var (basarili, hata) = await _leaveService.CancelLeaveAsync(id, userId);

            if (basarili) TempData["Success"] = "İzin talebiniz iptal edildi.";
            else TempData["Error"] = hata ?? "İzin iptali başarısız oldu.";

            return RedirectToAction(nameof(MyLeaves));
        }

        // ═══════════════ İZİN BELGESİ ═══════════════

        [HttpGet]
        public async Task<IActionResult> Belge(int id, CancellationToken ct)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            // Çalışan sadece kendi belgesini indirebilir; İK ve Yönetici hepsini
            var yetkili = User.IsInRole("İnsan Kaynakları") || User.IsInRole("Yönetici");

            if (!yetkili && !await _leaveService.IsOwnerAsync(id, userId))
                return Forbid();

            try
            {
                var (icerik, dosyaAdi) = await _belgeService.OlusturAsync(id, ct);
                return File(icerik, "application/pdf", dosyaAdi);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(MyLeaves));
            }
            catch
            {
                TempData["Error"] = "İzin belgesi oluşturulamadı.";
                return RedirectToAction(nameof(MyLeaves));
            }
        }

        // ═══════════════ AJAX — GÜN HESABI ═══════════════

        /// <summary>Tarih seçildikçe kaç iş günü düşeceğini canlı gösterir.</summary>
        [HttpGet]
        public async Task<IActionResult> GunHesapla(DateTime baslangic, DateTime bitis, bool cumartesiHaric = false)
        {
            if (bitis < baslangic)
                return Json(new { basarili = false, mesaj = "Bitiş tarihi başlangıçtan önce olamaz." });

            if ((bitis - baslangic).TotalDays > 365)
                return Json(new { basarili = false, mesaj = "Aralık çok geniş." });

            var userId = _userManager.GetUserId(User);
            var gun = await _leaveService.HesaplaIsGunuAsync(baslangic, bitis, cumartesiHaric);
            var takvimGun = (int)(bitis.Date - baslangic.Date).TotalDays + 1;

            var bakiye = await _leaveService.GetBalanceAsync(userId!, baslangic.Year);

            return Json(new
            {
                basarili = true,
                isGunu = gun,
                takvimGunu = takvimGun,
                kullanilabilir = bakiye.ProjectedRemainingDays,
                yeterli = gun <= bakiye.ProjectedRemainingDays,
                kalanSonrasi = bakiye.ProjectedRemainingDays - gun
            });
        }

        // ═══════════════ YARDIMCI ═══════════════

        private async Task DoldurAsync(CreateLeaveRequestViewModel model, string userId)
        {
            var turler = await _leaveService.GetLeaveTypesAsync();

            model.LeaveTypes = turler.Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.IsDeductedFromQuota ? x.Name : $"{x.Name} (haktan düşmez)",
                Selected = x.Id == model.LeaveTypeId
            }).ToList();

            model.QuotaDeductingTypes = turler.ToDictionary(x => x.Id, x => x.IsDeductedFromQuota);
            model.Balance = await _leaveService.GetBalanceAsync(userId, DateTime.Now.Year);

            var tatiller = await _tatilService.GetByYearAsync(DateTime.Now.Year);
            model.Tatiller = tatiller
                .GroupBy(t => t.Tarih.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.First().Ad);
        }
    }

}
