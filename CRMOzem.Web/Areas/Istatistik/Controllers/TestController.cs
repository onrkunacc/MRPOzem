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
        public async Task<IActionResult> Index(
            string? search,
            string? kategori,
            string? platform,
            string? girisTipi,
            CancellationToken ct)
        {
            try
            {
                var query = _context.Tests.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    query = query.Where(t =>
                        t.TestName.Contains(s) ||
                        (t.SutCode != null && t.SutCode.Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(kategori))
                    query = query.Where(t => t.Kategori == kategori);

                if (!string.IsNullOrWhiteSpace(platform))
                    query = query.Where(t => t.Platform == platform);

                if (!string.IsNullOrWhiteSpace(girisTipi))
                {
                    if (Enum.TryParse<TestGirisTipi>(girisTipi, out var tipi))
                        query = query.Where(t => t.GirisTipi == tipi);
                }

                var tests = await query
                    .OrderBy(t => t.Platform)
                    .ThenBy(t => t.Kategori)
                    .ThenBy(t => t.TestName)
                    .Select(t => new TestViewModel
                    {
                        TestID = t.TestID,
                        TestName = t.TestName,
                        SutCode = t.SutCode,
                        Kategori = t.Kategori,
                        Platform = t.Platform,
                        GirisTipi = t.GirisTipi,
                        KutuBasinaAdet = t.KutuBasinaAdet
                    })
                    .ToListAsync(ct);

                ViewBag.Kategoriler = await _context.Tests.AsNoTracking()
                    .Where(t => t.Kategori != null)
                    .Select(t => t.Kategori!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(ct);

                ViewBag.Platformlar = await _context.Tests.AsNoTracking()
                    .Where(t => t.Platform != null)
                    .Select(t => t.Platform!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(ct);

                ViewBag.SelectedSearch = search;
                ViewBag.SelectedKategori = kategori;
                ViewBag.SelectedPlatform = platform;
                ViewBag.SelectedGirisTipi = girisTipi;

                return View(tests);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Test listesi yüklenemedi: " + ex.Message;
                return View(new List<TestViewModel>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new TestCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TestCreateViewModel model, CancellationToken ct)
        {
            try
            {
                if (model.GirisTipi == TestGirisTipi.Kutu &&
                    (!model.KutuBasinaAdet.HasValue || model.KutuBasinaAdet.Value <= 0))
                {
                    ModelState.AddModelError(nameof(model.KutuBasinaAdet),
                        "Kutu tipinde giriş için 'Kutu Başına Adet' zorunludur.");
                }

                if (!ModelState.IsValid)
                    return View(model);

                var test = new Test
                {
                    TestName = model.TestName?.Trim() ?? "",
                    SutCode = model.SutCode?.Trim(),
                    Kategori = string.IsNullOrWhiteSpace(model.Kategori) ? null : model.Kategori.Trim(),
                    Platform = string.IsNullOrWhiteSpace(model.Platform) ? null : model.Platform.Trim(),
                    GirisTipi = model.GirisTipi,
                    KutuBasinaAdet = model.GirisTipi == TestGirisTipi.Kutu ? model.KutuBasinaAdet : null
                };

                _context.Tests.Add(test);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla eklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            try
            {
                var test = await _context.Tests.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TestID == id, ct);

                if (test == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                var model = new TestEditViewModel
                {
                    TestID = test.TestID,
                    TestName = test.TestName,
                    SutCode = test.SutCode,
                    Kategori = test.Kategori,
                    Platform = test.Platform,
                    GirisTipi = test.GirisTipi,
                    KutuBasinaAdet = test.KutuBasinaAdet
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TestEditViewModel model, CancellationToken ct)
        {
            try
            {
                if (model.GirisTipi == TestGirisTipi.Kutu &&
                    (!model.KutuBasinaAdet.HasValue || model.KutuBasinaAdet.Value <= 0))
                {
                    ModelState.AddModelError(nameof(model.KutuBasinaAdet),
                        "Kutu tipinde giriş için 'Kutu Başına Adet' zorunludur.");
                }

                if (!ModelState.IsValid)
                    return View(model);

                var existing = await _context.Tests.FirstOrDefaultAsync(t => t.TestID == model.TestID, ct);
                if (existing == null)
                {
                    TempData["Error"] = "Test bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                existing.TestName = model.TestName?.Trim() ?? "";
                existing.SutCode = model.SutCode?.Trim();
                existing.Kategori = string.IsNullOrWhiteSpace(model.Kategori) ? null : model.Kategori.Trim();
                existing.Platform = string.IsNullOrWhiteSpace(model.Platform) ? null : model.Platform.Trim();
                existing.GirisTipi = model.GirisTipi;
                existing.KutuBasinaAdet = model.GirisTipi == TestGirisTipi.Kutu ? model.KutuBasinaAdet : null;

                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Test başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata oluştu: " + ex.Message;
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

                TempData["SuccessMessage"] = "Test silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Silme hatası: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}