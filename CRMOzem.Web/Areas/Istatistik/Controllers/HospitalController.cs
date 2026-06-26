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
            var hospitals = await _context.Hospitals
                .AsNoTracking()
                .Include(h => h.HospitalBirimler)
                    .ThenInclude(hb => hb.Birim)
                .Include(h => h.TestTemplates)
                .Include(h => h.MonthlyVolumes)
                .OrderBy(x => x.HospitalName)
                .ToListAsync(ct);

            var rows = hospitals.Select(h => new HospitalRowVm
            {
                HospitalID = h.HospitalID,
                HospitalName = h.HospitalName,
                Address = h.Address,
                Phone = h.Phone,
                AktifBirimSayisi = h.HospitalBirimler.Count(x => x.Aktif),
                ToplamBirimKaydi = h.HospitalBirimler.Count,
                AktifBirimAdlari = h.HospitalBirimler
                    .Where(x => x.Aktif && x.Birim != null)
                    .Select(x => x.Birim!.BirimAdi)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),
                TestTemplateSayisi = h.TestTemplates?.Count ?? 0,
                SonVeriTarihi = h.MonthlyVolumes?.Any() == true
                    ? h.MonthlyVolumes.Max(v => (DateTime?)new DateTime(v.Year, v.Month, 1))
                    : null
            }).ToList();

            var vm = new HospitalListVm
            {
                Hospitals = rows,
                Summary = new HospitalListSummaryVm
                {
                    ToplamHastane = rows.Count,
                    AktifBirimToplam = rows.Sum(x => x.AktifBirimSayisi),
                    VerisiOlanHastane = rows.Count(x => x.SonVeriTarihi != null),
                    TestTanimliHastane = rows.Count(x => x.TestTemplateSayisi > 0)
                }
            };

            return View(vm);
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
