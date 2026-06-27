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
            try
            {
                var tests = await _context.Tests.AsNoTracking()
                    .OrderBy(x => x.TestName)
                    .ToListAsync(ct);

                var viewModel = tests.Select(t => new TestViewModel
                {
                    TestID = t.TestID,
                    TestName = t.TestName,
                    SutCode = t.SutCode,
                    GirisTipi = t.GirisTipi,
                    KutuBasinaAdet = t.KutuBasinaAdet
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Test listesi yüklenemedi: " + ex.Message;
                return View(new List<TestViewModel>());
            }
        }

        [HttpGet]
        public IActionResult Create() => View(new TestCreateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TestCreateViewModel model, CancellationToken ct)
        {
            // GirisTipi=Kutu ise KutuBasinaAdet zorunlu
            if (model.GirisTipi == TestGirisTipi.Kutu && !model.KutuBasinaAdet.HasValue)
            {
                ModelState.AddModelError(nameof(model.KutuBasinaAdet),
                    "Giriş tipi Kutu seçildiğinde kutu başına adet girilmelidir.");
            }

            if (!ModelState.IsValid) return View(model);

            try
            {
                var exists = await _context.Tests.AsNoTracking()
                    .AnyAsync(t => t.TestName.ToLower() == model.TestName.ToLower(), ct);

                if (exists)
                {
                    ModelState.AddModelError(nameof(model.TestName), "Bu test adı zaten kayıtlı.");
                    return View(model);
                }

                _context.Tests.Add(new Test
                {
                    TestName = model.TestName.Trim(),
                    SutCode = string.IsNullOrWhiteSpace(model.SutCode) ? null : model.SutCode.Trim(),
                    GirisTipi = model.GirisTipi,
                    KutuBasinaAdet = model.GirisTipi == TestGirisTipi.Kutu ? model.KutuBasinaAdet : null
                });

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla eklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hata: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            try
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
                    SutCode = test.SutCode,
                    GirisTipi = test.GirisTipi,
                    KutuBasinaAdet = test.KutuBasinaAdet
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Yükleme hatası: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TestEditViewModel model, CancellationToken ct)
        {
            if (model.GirisTipi == TestGirisTipi.Kutu && !model.KutuBasinaAdet.HasValue)
            {
                ModelState.AddModelError(nameof(model.KutuBasinaAdet),
                    "Giriş tipi Kutu seçildiğinde kutu başına adet girilmelidir.");
            }

            if (!ModelState.IsValid) return View(model);

            try
            {
                var test = await _context.Tests.FindAsync(new object?[] { model.TestID }, ct);
                if (test == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                test.TestName = model.TestName.Trim();
                test.SutCode = string.IsNullOrWhiteSpace(model.SutCode) ? null : model.SutCode.Trim();
                test.GirisTipi = model.GirisTipi;
                test.KutuBasinaAdet = model.GirisTipi == TestGirisTipi.Kutu ? model.KutuBasinaAdet : null;

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hata: " + ex.Message);
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
                TempData["Error"] = "Silme hatası: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Upload tarafı (otomatik test ekleme — varsayılan Adet, KutuBasinaAdet null)
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
                    SutCode = string.IsNullOrWhiteSpace(sutCode) ? null : sutCode.Trim(),
                    GirisTipi = TestGirisTipi.Adet  // upload'tan eklenen default Adet
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