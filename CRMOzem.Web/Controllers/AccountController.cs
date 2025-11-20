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

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
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
                ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifr" +
                    "e.");
                return View(model);
            }

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Yönetici"))
                return RedirectToAction("Index", "Admin", new { area = "Admin" });
            else if (roles.Contains("Muhasebe"))
                return RedirectToAction("Index", "Cari", new { area = "Muhasebe" });
            else if (roles.Contains("Finans"))
                return RedirectToAction("Index", "Dashboard", new { area = "Finans" });
            else if (roles.Contains("İnsan Kaynakları"))
                return RedirectToAction("Index", "Leave", new { area = "InsanKaynaklari" });
            else if (roles.Contains("İstatistik"))
                return RedirectToAction("Index", "Dashboard", new { area = "Istatistik" });
            else if (roles.Contains("İhale"))
                return RedirectToAction("Index", "FileTransferWeb", new { area = "" });
            else if (roles.Contains("Evrak"))
                return RedirectToAction("Index", "Document", new { area = "Evrak" });
         

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

            //Aynı mailde kullanıcı var mı ? 
            var existingEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanımda.");
                return View(model);
            }

            //Aynı kullanıcı adını kullanan kullanıcı var mı ? 
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



        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();  
            HttpContext.Response.Cookies.Delete(".AspNetCore.Identity.Application"); 
            return RedirectToAction("Login", "Account");
        }

        //[Authorize]
        public IActionResult AccessDenied()
        {
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
                // loglama mekanizmanız varsa burada loglayabilirsiniz
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

        // 📌 Kullanıcı Bilgilerini Güncelle
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

                // 📌 Mevcut Kullanıcı Bilgilerini Güncelle
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

                // 📌 Hataları Ekrana Bas
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
