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
    public class HospitalController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HospitalController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var hospitals = await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct);
            var viewModel = hospitals.Select(h => new HospitalViewModel
            {
                HospitalID = h.HospitalID,
                HospitalName = h.HospitalName
            }).ToList();
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Create() => View(new HospitalCreateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HospitalCreateViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                _context.Hospitals.Add(new Hospital { HospitalName = model.HospitalName });
                await _context.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = "Hastane başarıyla eklendi.";
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
            var hospital = await _context.Hospitals.FindAsync(new object?[] { id }, ct);
            if (hospital == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            return View(new HospitalEditViewModel
            {
                HospitalID = hospital.HospitalID,
                HospitalName = hospital.HospitalName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HospitalEditViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var hospital = await _context.Hospitals.FindAsync(new object?[] { model.HospitalID }, ct);
                if (hospital == null)
                {
                    TempData["Error"] = "Hastane bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                hospital.HospitalName = model.HospitalName;
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Hastane başarıyla güncellendi.";
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
                var hospital = await _context.Hospitals.FindAsync(new object?[] { id }, ct);
                if (hospital == null)
                {
                    TempData["Error"] = "Hastane bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Hospitals.Remove(hospital);
                await _context.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = "Hastane başarıyla silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
