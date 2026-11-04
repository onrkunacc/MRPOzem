using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.InsanKaynaklari.Controllers
{
    [Area("InsanKaynaklari")]
    [Authorize(Roles = "İnsan Kaynakları,Yönetici")]
    public class LeaveController : Controller
    {
        private readonly ILeaveService _leaveService;
        private readonly IResmiTatilService _tatilService;
        private readonly ILeaveBelgeService _belgeService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaveController(
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

        // ═══════════════════════════════════════════════════════
        //  TALEPLER
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> Index(int? yil, string? durum = "Pending")
        {
            var secilenYil = yil ?? DateTime.Now.Year;
            if (string.IsNullOrWhiteSpace(durum)) durum = null;

            var tumu = await _leaveService.GetAllAsync(secilenYil);

            var model = new LeaveAdminIndexVm
            {
                Year = secilenYil,
                StatusCode = durum,
                AvailableYears = await _leaveService.GetAvailableYearsAsync(),
                Requests = durum == null ? tumu : tumu.Where(r => r.Status?.Code == durum).ToList(),
                PendingCount = tumu.Count(r => r.Status?.Code == "Pending"),
                ApprovedCount = tumu.Count(r => r.Status?.Code == "Approved"),
                RejectedCount = tumu.Count(r => r.Status?.Code == "Rejected"),
                CancelledCount = tumu.Count(r => r.Status?.Code == "Cancelled"),
                ApprovedDays = tumu.Where(r => r.Status?.Code == "Approved").Sum(r => r.TotalDays),
                TodayOnLeaveCount = tumu.Count(r => r.Status?.Code == "Approved"
                                                 && r.StartDate.Date <= DateTime.Today
                                                 && r.EndDate.Date >= DateTime.Today)
            };

            // Bekleyen talepler için "onaylanırsa kalan" bilgisi
            var bekleyenler = model.Requests.Where(r => r.Status?.Code == "Pending").ToList();
            foreach (var r in bekleyenler)
            {
                var b = await _leaveService.GetBalanceAsync(r.UserId, r.StartDate.Year);
                model.KalanSonrasi[r.Id] = b.RemainingDays - r.TotalDays;
            }

            return View(model);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var talep = await _leaveService.GetByIdAsync(id);
            if (talep == null)
            {
                TempData["Error"] = "İzin talebi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Balance = await _leaveService.GetBalanceAsync(talep.UserId, talep.StartDate.Year);
            return View(talep);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? not, string? donus)
        {
            var userId = _userManager.GetUserId(User);
            var (basarili, hata) = await _leaveService.ApproveLeaveAsync(id, userId!, not);

            if (basarili) TempData["Success"] = "İzin talebi onaylandı.";
            else TempData["Error"] = hata ?? "Onay işlemi başarısız.";

            return donus == "detay"
                ? RedirectToAction(nameof(Detail), new { id })
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string gerekce, string? donus)
        {
            var userId = _userManager.GetUserId(User);
            var (basarili, hata) = await _leaveService.RejectLeaveAsync(id, userId!, gerekce);

            if (basarili) TempData["Success"] = "İzin talebi reddedildi.";
            else TempData["Error"] = hata ?? "Red işlemi başarısız.";

            return donus == "detay"
                ? RedirectToAction(nameof(Detail), new { id })
                : RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Belge(int id, CancellationToken ct)
        {
            try
            {
                var (icerik, dosyaAdi) = await _belgeService.OlusturAsync(id, ct);
                return File(icerik, "application/pdf", dosyaAdi);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Detail), new { id });
            }
            catch
            {
                TempData["Error"] = "İzin belgesi oluşturulamadı.";
                return RedirectToAction(nameof(Detail), new { id });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  BAKİYELER
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> Bakiyeler(int? yil)
        {
            var secilenYil = yil ?? DateTime.Now.Year;

            var model = new LeaveBalanceListVm
            {
                Year = secilenYil,
                AvailableYears = await _leaveService.GetAvailableYearsAsync(),
                Balances = await _leaveService.GetAllBalancesAsync(secilenYil)
            };

            return View(model);
        }

        /// <summary>İK elle hakediş girer.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BakiyeKaydet(string userId, int yil, int hakedis, int devreden, string? aciklama)
        {
            var (basarili, hata) = await _leaveService.SetEntitlementAsync(userId, yil, hakedis, devreden, aciklama);

            if (basarili) TempData["Success"] = "Hakediş kaydedildi.";
            else TempData["Error"] = hata ?? "Hakediş kaydedilemedi.";

            return RedirectToAction(nameof(Bakiyeler), new { yil });
        }

        /// <summary>Tek çalışanın hakedişini kıdeminden hesaplar.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OtomatikHesapla(string userId, int yil, bool devriAktar = true)
        {
            var (basarili, hata, hakedis) = await _leaveService.OtomatikHakedisAsync(userId, yil, devriAktar);

            if (basarili) TempData["Success"] = $"Kıdeme göre {hakedis} gün hakediş hesaplandı.";
            else TempData["Error"] = hata ?? "Otomatik hesaplama yapılamadı.";

            return RedirectToAction(nameof(Bakiyeler), new { yil });
        }

        /// <summary>Tüm aktif çalışanlar için yılı toplu oluşturur.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YilOlustur(int yil, bool devriAktar = true, bool manuelleriEz = false)
        {
            var (olusturulan, guncellenen, atlanan) =
                await _leaveService.YilToplamOlusturAsync(yil, devriAktar, manuelleriEz);

            TempData["Success"] =
                $"{yil} yılı hazırlandı — {olusturulan} yeni kayıt, {guncellenen} güncelleme" +
                (atlanan > 0 ? $", {atlanan} elle girilmiş kayıt korundu." : ".");

            return RedirectToAction(nameof(Bakiyeler), new { yil });
        }

        // ═══════════════════════════════════════════════════════
        //  PERSONEL ÖZLÜK
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> Personel(bool sadeceAktif = false)
        {
            ViewBag.SadeceAktif = sadeceAktif;
            var model = await _leaveService.GetPersonelListAsync(sadeceAktif);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> PersonelDuzenle(string id)
        {
            var model = await _leaveService.GetPersonelAsync(id);
            if (model == null)
            {
                TempData["Error"] = "Çalışan bulunamadı.";
                return RedirectToAction(nameof(Personel));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonelDuzenle(PersonelEditVm model)
        {
            if (!ModelState.IsValid)
            {
                var tazele = await _leaveService.GetPersonelAsync(model.UserId);
                if (tazele != null)
                {
                    model.FullName = tazele.FullName;
                    model.UserName = tazele.UserName;
                    model.Bakiyeler = tazele.Bakiyeler;
                }
                return View(model);
            }

            var (basarili, hata) = await _leaveService.UpdatePersonelAsync(model);

            if (!basarili)
            {
                ModelState.AddModelError("", hata ?? "Kayıt güncellenemedi.");
                var tazele = await _leaveService.GetPersonelAsync(model.UserId);
                if (tazele != null)
                {
                    model.FullName = tazele.FullName;
                    model.UserName = tazele.UserName;
                    model.Bakiyeler = tazele.Bakiyeler;
                }
                return View(model);
            }

            TempData["Success"] = "Özlük bilgileri güncellendi.";
            return RedirectToAction(nameof(Personel));
        }

        // ═══════════════════════════════════════════════════════
        //  TAKVİM
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> Takvim(int? yil, int? ay)
        {
            var y = yil ?? DateTime.Now.Year;
            var a = ay ?? DateTime.Now.Month;

            var model = await _leaveService.GetCalendarAsync(y, a);
            return View(model);
        }

        // ═══════════════════════════════════════════════════════
        //  RESMİ TATİLLER
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> ResmiTatiller(int? yil)
        {
            var secilenYil = yil ?? DateTime.Now.Year;

            var model = new ResmiTatilListVm
            {
                Yil = secilenYil,
                AvailableYears = await _tatilService.GetAvailableYearsAsync(),
                Tatiller = await _tatilService.GetByYearAsync(secilenYil)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SabitTatilleriOlustur(int yil)
        {
            var (eklenen, atlanan) = await _tatilService.SabitTatilleriOlusturAsync(yil);

            TempData["Success"] = eklenen > 0
                ? $"{eklenen} sabit tatil eklendi" + (atlanan > 0 ? $", {atlanan} tanesi zaten kayıtlıydı." : ".")
                : "Sabit tatillerin tamamı zaten kayıtlı. Dini bayramları elle eklemeniz gerekiyor.";

            return RedirectToAction(nameof(ResmiTatiller), new { yil });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TatilEkle(DateTime baslangic, DateTime bitis, string ad, bool tamGun, string? aciklama, int yil)
        {
            var (eklenen, atlanan, hata) = await _tatilService.AralikEkleAsync(baslangic, bitis, ad, tamGun, aciklama);

            if (hata != null) TempData["Error"] = hata;
            else TempData["Success"] = $"{eklenen} gün eklendi" + (atlanan > 0 ? $", {atlanan} gün zaten kayıtlıydı." : ".");

            return RedirectToAction(nameof(ResmiTatiller), new { yil });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TatilSil(int id, int yil)
        {
            var (basarili, hata) = await _tatilService.SilAsync(id);

            if (basarili) TempData["Success"] = "Tatil kaydı silindi.";
            else TempData["Error"] = hata ?? "Silme işlemi başarısız.";

            return RedirectToAction(nameof(ResmiTatiller), new { yil });
        }
    }
}
