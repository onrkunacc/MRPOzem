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

        
        public override async Task OnActionExecutionAsync(
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
            Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
        {
            try
            {
                var acik = await _leaveService.GetAllAsync(null, "SupervisorApproved");
                ViewBag.IkOnayindaCount = acik.Count;
            }
            catch
            {
                // Sayaç bilgi amaçlı — alınamazsa sayfa yine açılsın
                ViewBag.IkOnayindaCount = null;
            }

            await next();
        }

        // ═══════════════════════════════════════════════════════
        //  TALEPLER
        // ═══════════════════════════════════════════════════════
        //  İki aşamalı akış:
        //    Pending            → çalışanın yöneticisinde (İK henüz sırada değil)
        //    SupervisorApproved → İK'da  ← İK'nın asıl iş kuyruğu
        //
        //  Bu yüzden varsayılan süzgeç "SupervisorApproved".

        public async Task<IActionResult> Index(int? yil, string? durum = "SupervisorApproved")
        {
            var secilenYil = yil ?? DateTime.Now.Year;
            if (string.IsNullOrWhiteSpace(durum)) durum = null;

            var tumu = await _leaveService.GetAllAsync(secilenYil);

            // Süzgeç "acik" ise iki aşamayı birlikte göster
            List<LeaveRequest> gosterilecek;
            if (durum == null)
                gosterilecek = tumu;
            else if (durum == "acik")
                gosterilecek = tumu.Where(r => r.Status?.Code == "Pending"
                                            || r.Status?.Code == "SupervisorApproved").ToList();
            else
                gosterilecek = tumu.Where(r => r.Status?.Code == durum).ToList();

            var model = new LeaveAdminIndexVm
            {
                Year = secilenYil,
                StatusCode = durum,
                AvailableYears = await _leaveService.GetAvailableYearsAsync(),
                Requests = gosterilecek,

                YoneticiOnayindaCount = tumu.Count(r => r.Status?.Code == "Pending"),
                IkOnayindaCount = tumu.Count(r => r.Status?.Code == "SupervisorApproved"),
                ApprovedCount = tumu.Count(r => r.Status?.Code == "Approved"),
                RejectedCount = tumu.Count(r => r.Status?.Code == "Rejected"),
                CancelledCount = tumu.Count(r => r.Status?.Code == "Cancelled"),

                YoneticisizCount = tumu.Count(r => r.YoneticisizGecildi
                                                && (r.Status?.Code == "Pending"
                                                 || r.Status?.Code == "SupervisorApproved")),

                ApprovedDays = tumu.Where(r => r.Status?.Code == "Approved").Sum(r => r.TotalDays),
                TodayOnLeaveCount = tumu.Count(r => r.Status?.Code == "Approved"
                                                 && r.StartDate.Date <= DateTime.Today
                                                 && r.EndDate.Date >= DateTime.Today)
            };

            // Açık talepler için "onaylanırsa kalan" bilgisi.
            // Aynı çalışanın birkaç talebi olabilir — bakiyeyi kişi başına
            // bir kez çekip tekrar tekrar sorgu atmıyoruz.
            var acikOlanlar = model.Requests
                .Where(r => r.Status?.Code == "Pending" || r.Status?.Code == "SupervisorApproved")
                .ToList();

            var bakiyeOnbellek = new Dictionary<string, LeaveBalanceVm>();

            foreach (var r in acikOlanlar)
            {
                var anahtar = $"{r.UserId}|{r.StartDate.Year}";
                if (!bakiyeOnbellek.TryGetValue(anahtar, out var b))
                {
                    b = await _leaveService.GetBalanceAsync(r.UserId, r.StartDate.Year);
                    bakiyeOnbellek[anahtar] = b;
                }
                model.KalanSonrasi[r.Id] = b.RemainingDays - r.TotalDays;
            }

            return View(model);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var baglam = await _leaveService.GetOnayBaglamiAsync(id);
            if (baglam?.Talep == null)
            {
                TempData["Error"] = "İzin talebi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            return View(baglam);
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
        //  ORGANİZASYON ŞEMASI
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> Organizasyon()
        {
            var model = await _leaveService.GetOrganizasyonAsync();
            return View(model);
        }

        /// <summary>Tek çalışanın yöneticisini atar ya da kaldırır.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YoneticiAta(string userId, string? supervisorUserId)
        {
            var atayanId = _userManager.GetUserId(User);
            var (basarili, hata) = await _leaveService.SetSupervisorAsync(userId, supervisorUserId, atayanId!);

            if (basarili)
                TempData["Success"] = string.IsNullOrWhiteSpace(supervisorUserId)
                    ? "Yönetici bağı kaldırıldı. Bu çalışanın yeni talepleri doğrudan İK onayına düşecek."
                    : "Yönetici atandı. Bu çalışanın yeni talepleri önce yöneticisine gidecek.";
            else
                TempData["Error"] = hata ?? "Yönetici atanamadı.";

            return RedirectToAction(nameof(Organizasyon));
        }

        /// <summary>Bir yöneticinin tüm astlarını başka yöneticiye devreder.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AstlariDevret(string eskiSupervisorUserId, string? yeniSupervisorUserId)
        {
            var atayanId = _userManager.GetUserId(User);
            var (basarili, hata, tasinan) = await _leaveService.AstlariDevretAsync(
                eskiSupervisorUserId, yeniSupervisorUserId, atayanId!);

            if (basarili)
            {
                TempData["Success"] = string.IsNullOrWhiteSpace(yeniSupervisorUserId)
                    ? $"{tasinan} çalışanın yönetici bağı kaldırıldı."
                    : $"{tasinan} çalışan yeni yöneticiye devredildi.";

                if (!string.IsNullOrWhiteSpace(hata))
                    TempData["Error"] = hata;   // kısmi uyarı
            }
            else
            {
                TempData["Error"] = hata ?? "Devir işlemi başarısız.";
            }

            return RedirectToAction(nameof(Organizasyon));
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

            if (basarili) TempData["Success"] = "Hakediş elle kaydedildi — toplu hesaplama bu kaydı ezmeyecek.";
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
                await TazeleAsync(model);
                return View(model);
            }

            var (basarili, hata) = await _leaveService.UpdatePersonelAsync(model);

            if (!basarili)
            {
                ModelState.AddModelError("", hata ?? "Kayıt güncellenemedi.");
                await TazeleAsync(model);
                return View(model);
            }

            TempData["Success"] = "Özlük bilgileri güncellendi.";
            return RedirectToAction(nameof(Personel));
        }

        /// <summary>Doğrulama hatasında salt okunur alanları geri doldurur.</summary>
        private async Task TazeleAsync(PersonelEditVm model)
        {
            var tazele = await _leaveService.GetPersonelAsync(model.UserId);
            if (tazele == null) return;

            model.FullName = tazele.FullName;
            model.UserName = tazele.UserName;
            model.Bakiyeler = tazele.Bakiyeler;
            model.YoneticiAdi = tazele.YoneticiAdi;
            model.AstSayisi = tazele.AstSayisi;
            model.Kidem = tazele.Kidem;
            model.Yas = tazele.Yas;
            model.BuYilHakedis = tazele.BuYilHakedis;
            model.HakedisAciklamasi = tazele.HakedisAciklamasi;
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