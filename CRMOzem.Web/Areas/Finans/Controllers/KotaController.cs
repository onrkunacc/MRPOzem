using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Finans.Controllers
{
    [Area("Finans")]
    [Authorize(Roles = "Yönetici,KotaTakip")]
    public class KotaController : Controller
    {
        private readonly IKotaService _service;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public KotaController(IKotaService service, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _service = service;
            _userManager = userManager;
            _db = db;   
        }


        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            // Tüm kotaları çek
            var kotalar = await _db.Kotalar
                .AsNoTracking()
                .OrderByDescending(k => k.KotaID)
                .ToListAsync(ct);

            var kotaIdler = kotalar.Select(k => k.KotaID).ToList();

            // İlgili tüm hareketleri tedarikçi bilgisiyle çek
            var hareketler = await _db.KotaHareketleri
                .AsNoTracking()
                .Include(h => h.Tedarikci)
                .Where(h => kotaIdler.Contains(h.KotaID))
                .ToListAsync(ct);

            var giderler = hareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gider).ToList();
            var gelirler = hareketler.Where(h => h.HareketTuru == KotaHareketTuru.Gelir).ToList();

            // KotaRowVm listesi oluştur (Dashboard'daki ile aynı mantık)
            var model = kotalar.Select(k =>
            {
                var kGider = giderler.Where(h => h.KotaID == k.KotaID).ToList();
                var kGelir = gelirler.Where(h => h.KotaID == k.KotaID).ToList();

                return new KotaRowVm
                {
                    KotaID = k.KotaID,
                    KotaKodu = k.KotaKodu,
                    IsAdi = k.IsAdi,
                    Sorumlu = k.Sorumlu,
                    Bolge = k.Bolge,
                    ProformaUsd = k.ProformaTutarUsd ?? 0m,
                    Durum = k.Durum,
                    BaslamaTarihi = k.BaslamaTarihi,
                    BitisTarihi = k.BitisTarihi,

                    AbbottGiderUsd = kGider
                        .Where(h => h.Tedarikci?.Grup == TedarikciGrup.Abbott)
                        .Sum(h => h.UsdKarsiligi),

                    ThirdPartyGiderUsd = kGider
                        .Where(h => h.Tedarikci?.Grup == TedarikciGrup.ThirdParty)
                        .Sum(h => h.UsdKarsiligi),

                    DigerGiderUsd = kGider
                        .Where(h => h.Tedarikci?.Grup != TedarikciGrup.Abbott
                                 && h.Tedarikci?.Grup != TedarikciGrup.ThirdParty)
                        .Sum(h => h.UsdKarsiligi),

                    GelirUsd = kGelir.Sum(h => h.UsdKarsiligi)
                };
            })
            .OrderByDescending(r => r.ProformaUsd)
            .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            await LoadHospitalsAsync(ct);
            return View(new KotaCreateVm());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KotaCreateVm vm, CancellationToken ct)
        {
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => new { Field = x.Key, Errors = x.Value!.Errors.Select(e => e.ErrorMessage) })
                    .ToList();

                TempData["Error"] = "Doğrulama hatası: " + string.Join(" | ",
                    errors.SelectMany(e => e.Errors.Select(err => $"{e.Field}: {err}")));

                return View(vm);
            }

            try
            {
                var userId = _userManager.GetUserId(User);
                await _service.CreateAsync(vm, userId, ct);
                TempData["SuccessMessage"] = "Kota oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var vm = await _service.GetByIdAsync(id, ct);
            if (vm == null)
            {
                TempData["Error"] = "Kota bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            await LoadHospitalsAsync(ct);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(KotaEditVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                await LoadHospitalsAsync(ct);        // ← formu tekrar açarken hastane listesi lazım
                return View(vm);
            }
            try
            {
                await _service.UpdateAsync(vm, ct);
                TempData["SuccessMessage"] = "Kota güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var mesaj = ex.Message;
                var iex = ex.InnerException;
                while (iex != null)
                {
                    mesaj += " → " + iex.Message;
                    iex = iex.InnerException;
                }
                TempData["Error"] = "Hata: " + mesaj;
                await LoadHospitalsAsync(ct);        // ← burada da
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                await _service.DeleteAsync(id, ct);
                TempData["SuccessMessage"] = "Kota silindi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Silme hatası: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadHospitalsAsync(CancellationToken ct)
        {
            ViewBag.Hospitals = await _db.Hospitals.AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);
        }

        // ═══════════════════════════════════════════════
        // AJAX: Hızlı hastane ekle
        // POST /Finans/Kota/AddHospitalAjax
        // ═══════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHospitalAjax(string hospitalName, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hospitalName))
                    return Json(new { success = false, message = "Hastane adı boş olamaz." });

                var ad = hospitalName.Trim();

                // Aynı isimde var mı kontrol
                var mevcut = await _db.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalName == ad, ct);
                if (mevcut != null)
                {
                    return Json(new
                    {
                        success = true,
                        exists = true,
                        hospitalId = mevcut.HospitalID,
                        hospitalName = mevcut.HospitalName,
                        message = "Bu isimde hastane zaten var, işaretlendi."
                    });
                }

                var yeni = new CRMOzem.Core.Models.Hospital
                {
                    HospitalName = ad
                };
                _db.Hospitals.Add(yeni);
                await _db.SaveChangesAsync(ct);

                return Json(new
                {
                    success = true,
                    exists = false,
                    hospitalId = yeni.HospitalID,
                    hospitalName = yeni.HospitalName,
                    message = "Hastane eklendi."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }
    }
}