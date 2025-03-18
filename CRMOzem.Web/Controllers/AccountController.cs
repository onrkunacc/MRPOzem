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
        public AccountController(IUserService userService, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, Business.Interfaces.IEmailSender emailSender)
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

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre.");
                return View(model);
            }

            // Kullanıcının rollerini al
            var roles = await _userManager.GetRolesAsync(user);

           
            if (roles.Contains("Yönetici"))
            {
                return RedirectToAction("Index", "Admin", new { area = "Admin" });
            }
            else if (roles.Contains("Muhasebe"))
            {
                return RedirectToAction("Index", "Cari", new { area = "Muhasebe" });
            }
            else if (roles.Contains("Finans"))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Finans" });
            }
            else if (roles.Contains("İnsan Kaynakları"))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "InsanKaynaklari" });
            }
            else if (roles.Contains("İhale"))
            {
                return RedirectToAction("Index", "BidDocument", new { area = "Ihale" });
            }
            else if (roles.Contains("Evrak"))
            {
                return RedirectToAction("Index", "Document", new { area = "Evrak" });
            }

            // Eğer yalnızca Kullanıcı rolü varsa Home/Index'e yönlendir
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
            Console.WriteLine("Çıkış yapılıyor");
            await _signInManager.SignOutAsync();  
            HttpContext.Response.Cookies.Delete(".AspNetCore.Identity.Application"); 
            Console.WriteLine("Çıkış yapıldı");
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
                return View();
            }

            // ✅ Şifre sıfırlama token oluştur
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // ✅ Kullanıcıya şifre sıfırlama linki içeren bir e-posta gönder
            var resetLink = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);
            await _emailSender.SendEmailAsync(user.Email, "Şifre Sıfırlama", $"Şifrenizi sıfırlamak için <a href='{resetLink}'>buraya tıklayın</a>.");

            TempData["Message"] = "Şifre sıfırlama bağlantınız e-posta adresinize gönderildi.";
            return RedirectToAction("ForgotPassword");
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
