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

        // Listeleme
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var hospitals = await _context.Hospitals.ToListAsync();

                var viewModel = hospitals.Select(h => new HospitalViewModel
                {
                    HospitalID = h.HospitalID,
                    HospitalName = h.HospitalName
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Yeni Hastane Formu
        [HttpGet]
        public IActionResult Create()
        {
            return View(new HospitalCreateViewModel());
        }

        // Yeni Hastane Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HospitalCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var hospital = new Hospital
                {
                    HospitalName = model.HospitalName
                };

                _context.Hospitals.Add(hospital);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Hastane başarıyla eklendi.";
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
            var hospital = await _context.Hospitals.FindAsync(id);
            if (hospital == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction("Index");
            }

            var viewModel = new HospitalEditViewModel
            {
                HospitalID = hospital.HospitalID,
                HospitalName = hospital.HospitalName
            };

            return View(viewModel);
        }

        // Düzenleme Kaydet
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HospitalEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var hospital = await _context.Hospitals.FindAsync(model.HospitalID);
                if (hospital == null)
                {
                    TempData["Error"] = "Hastane bulunamadı.";
                    return RedirectToAction("Index");
                }

                hospital.HospitalName = model.HospitalName;

                _context.Hospitals.Update(hospital);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Hastane başarıyla güncellendi.";
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
                var hospital = await _context.Hospitals.FindAsync(id);
                if (hospital == null)
                {
                    TempData["Error"] = "Hastane bulunamadı.";
                    return RedirectToAction("Index");
                }

                _context.Hospitals.Remove(hospital);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Hastane başarıyla silindi.";
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
