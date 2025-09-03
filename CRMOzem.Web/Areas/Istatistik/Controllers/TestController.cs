using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik")]
    public class TestController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TestController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Listeleme
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var tests = await _context.Tests.ToListAsync();

                var viewModel = tests.Select(t => new TestViewModel
                {
                    TestID = t.TestID,
                    TestName = t.TestName
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Yeni Test Formu
        [HttpGet]
        public IActionResult Create()
        {
            return View(new TestCreateViewModel());
        }

        // Yeni Test Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TestCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var test = new Test
                {
                    TestName = model.TestName
                };

                _context.Tests.Add(test);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Test başarıyla eklendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return View(model);
            }
        }

        // Düzenleme Formu
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var test = await _context.Tests.FindAsync(id);
            if (test == null)
            {
                TempData["Error"] = "Test bulunamadı.";
                return RedirectToAction("Index");
            }

            var viewModel = new TestEditViewModel
            {
                TestID = test.TestID,
                TestName = test.TestName
            };

            return View(viewModel);
        }

        // Düzenleme Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TestEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var test = await _context.Tests.FindAsync(model.TestID);
                if (test == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction("Index");
                }

                test.TestName = model.TestName;

                _context.Tests.Update(test);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Test başarıyla güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return View(model);
            }
        }

        // Silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var test = await _context.Tests.FindAsync(id);
                if (test == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction("Index");
                }

                _context.Tests.Remove(test);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Test başarıyla silindi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
