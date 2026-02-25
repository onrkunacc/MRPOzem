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

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var tests = await _context.Tests.AsNoTracking()
                .OrderBy(x => x.TestName)
                .ToListAsync(ct);

            var viewModel = tests.Select(t => new TestViewModel
            {
                TestID = t.TestID,
                TestName = t.TestName,
                SutCode = t.SutCode
            }).ToList();

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Create() => View(new TestCreateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TestCreateViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var exists = await _context.Tests.AsNoTracking()
                    .AnyAsync(t => t.TestName.ToLower() == model.TestName.ToLower(), ct);

                if (exists)
                {
                    ModelState.AddModelError("TestName", "Bu test adı zaten kayıtlı.");
                    return View(model);
                }

                _context.Tests.Add(new Test
                {
                    TestName = model.TestName.Trim(),
                    SutCode = string.IsNullOrWhiteSpace(model.SutCode) ? null : model.SutCode.Trim()
                });

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla eklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Hata: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var test = await _context.Tests.FindAsync(new object?[] { id }, ct);
            if (test == null)
            {
                TempData["Error"] = "Test bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            return View(new TestEditViewModel
            {
                TestID = test.TestID,
                TestName = test.TestName,
                SutCode = test.SutCode
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TestEditViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var test = await _context.Tests.FindAsync(new object?[] { model.TestID }, ct);
                if (test == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // İstersen burada isim duplicate kontrolü de ekleyebiliriz.
                test.TestName = model.TestName.Trim();
                test.SutCode = string.IsNullOrWhiteSpace(model.SutCode) ? null : model.SutCode.Trim();

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Hata: {ex.Message}");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                var test = await _context.Tests.FindAsync(new object?[] { id }, ct);
                if (test == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Tests.Remove(test);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // Upload tarafı için (opsiyonel sutCode destekli)
        [HttpPost]
        public async Task<IActionResult> CreateFromUpload(string testName, string? sutCode, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(testName))
                    return Json(new { success = false, message = "Test adı boş olamaz." });

                var exists = await _context.Tests.AsNoTracking()
                    .AnyAsync(t => t.TestName == testName, ct);

                if (exists)
                    return Json(new { success = false, message = "Bu test zaten kayıtlı." });

                var test = new Test
                {
                    TestName = testName.Trim(),
                    SutCode = string.IsNullOrWhiteSpace(sutCode) ? null : sutCode.Trim()
                };

                _context.Tests.Add(test);
                await _context.SaveChangesAsync(ct);

                return Json(new { success = true, testId = test.TestID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSutCode(int id, string? sutCode, CancellationToken ct)
        {
            try
            {
                var test = await _context.Tests.FindAsync(new object?[] { id }, ct);
                if (test == null)
                    return Json(new { success = false, message = "Test bulunamadı." });

                // normalize
                var normalized = string.IsNullOrWhiteSpace(sutCode) ? null : sutCode.Trim();

                test.SutCode = normalized;
                await _context.SaveChangesAsync(ct);

                return Json(new { success = true, sutCode = test.SutCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}