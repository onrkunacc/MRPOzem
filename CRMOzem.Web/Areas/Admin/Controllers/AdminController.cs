using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles ="Yönetici")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync("Kullanıcı");
                ViewBag.AktifKullaniciSayisi = usersInRole.Count;

                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                var toplamMasraf = _context.ExpenseReports
                    .Where(x => x.MonthYear.Year == currentYear && x.MonthYear.Month == currentMonth)
                    .Sum(x => x.TotalAmount);

                ViewBag.ToplamAylikMasraf = toplamMasraf;

                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
