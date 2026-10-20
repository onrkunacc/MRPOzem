using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Business.Interfaces.IEmailSender _emailSender;

        public AccountController(
            IUserService userService,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            Business.Interfaces.IEmailSender emailSender)
        {
            _userService = userService;
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        // ⚠️ GEÇMİŞ: Login GET her durumda View dönüyordu.
        // Artık authenticated kullanıcı Login'e düşerse → anasayfaya yönlendir.
        // Bu PWA'da en kritik düzeltmelerden biri: AccessDenied → "Giriş sayfası" dead-end'ini kırar.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? reason = null)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                // Zaten giriş yapmış ve "Login" tuşuna bastıysa → anasayfaya götür
                return RedirectToAction("Index", "Home");
            }

            if (reason == "norole")
            {
                TempData["Message"] = "Hesabınıza henüz rol atanmamış. Lütfen yöneticinizle görüşün.";
            }
            else if (reason == "logout")
            {
                TempData["Message"] = "Oturumunuz kapatıldı.";
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null)
            {
                ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre.");
                return View(model);
            }

            if (!user.IsApproved)
            {
                ModelState.AddModelError("", "Hesabınız henüz yönetici tarafından onaylanmadı.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre.");
                return View(model);
            }

            var roles = await _userManager.GetRolesAsync(user);

            // ⚠️ Rol atanmamış kullanıcı → login yapma, SignOut ve mesaj göster
            if (roles.Count == 0)
            {
                await _signInManager.SignOutAsync();
                TempData["Message"] = "Hesabınıza henüz rol atanmamış. Lütfen yöneticinizle görüşün.";
                return RedirectToAction("Login");
            }

            // Rol bazlı landing — Program.cs middleware ile UYUMLU TUTULDU
            if (roles.Contains("Yönetici"))
                return Redirect("/Admin/Admin/Index");
            if (roles.Contains("Muhasebe"))
                return Redirect("/Muhasebe/Cari/Index");
            if (roles.Contains("KotaTakip"))
                return Redirect("/Finans/Kota/Index");
            if (roles.Contains("İnsan Kaynakları"))
                return Redirect("/InsanKaynaklari/Leave/Index");
            if (roles.Contains("İstatistik") || roles.Contains("İstatistikYonetici")
                || roles.Contains("İstatistikGiris") || roles.Contains("DepoGiris")
                || roles.Contains("IstatistikRapor") || roles.Contains("IstatistikGoruntule"))
                return Redirect("/Istatistik/Dashboard/Index");
            if (roles.Contains("İhale") || roles.Contains("IhaleYonetici")
                || roles.Contains("IhaleGiris") || roles.Contains("IhaleGoruntule"))
                return Redirect("/Ihale/Ihale/Index");
            if (roles.Contains("Evrak"))
                return Redirect("/Evrak/Document/Index");
            if (roles.Contains("IT"))
                return Redirect("/IT/Inventory/Index");
            if (roles.Contains("Aplikasyon"))
                return Redirect("/Istatistik/Report/Index");
            if (roles.Contains("Lojistik"))
                return Redirect("/Lojistik/Dashboard/Index");

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existingEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanımda.");
                return View(model);
            }

            var existingUserName = await _userManager.FindByNameAsync(model.UserName);
            if (existingUserName != null)
            {
                ModelState.AddModelError("UserName", "Bu kullanıcı adı zaten kullanımda.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Title = model.Title,
                Email = model.Email,
                UserName = model.UserName,
                PhoneNumber = model.PhoneNumber,
                IsApproved = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Kullanıcı");
                TempData["Message"] = "Kaydınız alınmıştır. Yönetici onayladıktan sonra giriş yapabilirsiniz.";
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ⚠️ LOGOUT — hem GET hem POST kabul eder (PWA'daki AccessDenied linki için GET şart)
        // Önceden sadece GET + yanlış cookie adı siliniyordu (.AspNetCore.Identity.Application
        // ama gerçek adı CRMOzem.Auth). Artık TÜM auth cookie'leri siliniyor.
        [Authorize]
        [AcceptVerbs("GET", "POST")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            // Tüm auth-ilgili cookie'leri temizle (PWA'da takılı kalma problemine karşı)
            foreach (var cookieName in Request.Cookies.Keys.ToList())
            {
                if (cookieName.StartsWith("CRMOzem.", StringComparison.OrdinalIgnoreCase) ||
                    cookieName.StartsWith(".AspNetCore.", StringComparison.OrdinalIgnoreCase) ||
                    cookieName.StartsWith("Identity.", StringComparison.OrdinalIgnoreCase))
                {
                    Response.Cookies.Delete(cookieName);
                    Response.Cookies.Delete(cookieName, new CookieOptions { Path = "/" });
                }
            }

            // Cache'i de temizlemek için anti-cache header'ları
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return RedirectToAction("Login", "Account", new { reason = "logout" });
        }

        // ⚠️ AccessDenied — AllowAnonymous eklendi
        // Önceden [Authorize] filtresi hiç yoktu ama middleware'in bazı durumlarda cookie
        // geçersizse buraya tekrar düşmesine karşı garantili erişim için eklendi.
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            // Login + AccessDenied sayfaları ASLA cache'lenmesin (PWA stale state'e karşı)
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            ViewBag.IsAuthenticated = User?.Identity?.IsAuthenticated == true;
            ViewBag.UserName = User?.Identity?.Name;
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["Message"] = "E-posta adresiniz sistemde bulunamadı.";
                return View(model);
            }

            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);

                string emailBody = $@"
                <div style='font-family:Arial, sans-serif; padding:20px; background-color:#f4f4f4;'>
                    <div style='max-width:600px; margin:auto; background-color:#fff; padding:30px; border-radius:12px; box-shadow:0 4px 12px rgba(0,0,0,0.1);'>
                        <h2 style='color:#2c3e50;'>🔐 Şifre Sıfırlama İsteği</h2>
                        <p>Merhaba <strong>{user.FullName}</strong>,</p>
                        <p>Şifrenizi sıfırlamak için aşağıdaki butona tıklayabilirsiniz:</p>
                        <div style='text-align:center; margin:40px 0;'>
                            <a href='{resetLink}'
                               style='display:inline-block; background-color:#28a745; color:white; padding:15px 30px;
                                      font-size:16px; font-weight:bold; text-decoration:none; border-radius:8px;
                                      box-shadow:0 2px 6px rgba(0,0,0,0.2); transition:all 0.3s ease;'>
                                Şifremi Sıfırla
                            </a>
                        </div>
                        <p>Bu isteği siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                        <p style='color:#888;'>İyi günler dileriz, <br/><strong>ÖZEM</strong></p>
                    </div>
                </div>";

                await _emailSender.SendEmailAsync(user.Email, "Şifre Sıfırlama Bağlantınız", emailBody);

                TempData["Message"] = "Şifre sıfırlama bağlantınız e-posta adresinize gönderildi.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "İşlem sırasında bir hata oluştu. Lütfen tekrar deneyin.";
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                ModelState.AddModelError("", "Geçersiz şifre sıfırlama bağlantısı.");
                return View();
            }

            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["Message"] = "Geçersiz işlem.";
                return RedirectToAction("Login");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                TempData["Message"] = "Şifreniz başarıyla güncellendi.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound("Kullanıcı bulunamadı.");

                var model = new ProfileViewModel
                {
                    FullName = user.FullName,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Title = user.Title,
                    CardNumber = user.CardNumber
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Profil bilgileri yüklenirken bir hata oluştu.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Lütfen eksik veya hatalı alanları kontrol edin.";
                    return View(model);
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                    return RedirectToAction("Index", "Home");
                }

                user.FullName = model.FullName;
                user.UserName = model.UserName;
                user.PhoneNumber = model.PhoneNumber;
                user.Title = model.Title;
                user.CardNumber = model.CardNumber;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Profiliniz başarıyla güncellendi.";
                    return RedirectToAction(nameof(Profile));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Profil güncellenirken bir hata oluştu.";
                return View(model);
            }
        }
    }
}