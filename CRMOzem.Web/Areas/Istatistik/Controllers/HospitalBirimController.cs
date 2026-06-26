using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "İstatistik,Yönetici")]
    public class HospitalBirimController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HospitalBirimController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task LoadHospitalsAsync(int? selectedHospitalId, CancellationToken ct)
        {
            ViewBag.Hospitals = await _context.Hospitals.AsNoTracking()
                .OrderBy(x => x.HospitalName)
                .Select(x => new SelectListItem
                {
                    Value = x.HospitalID.ToString(),
                    Text = x.HospitalName
                })
                .ToListAsync(ct);

            ViewBag.SelectedHospitalId = selectedHospitalId;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, CancellationToken ct)
        {
            await LoadHospitalsAsync(hospitalId, ct);

            // Genel özet — hastane seçilmemiş olsa bile göster
            var bugun = DateTime.Today;
            var tumKayitlar = await _context.HospitalBirimler.AsNoTracking()
                .Select(x => new { x.HospitalID, x.Aktif, x.BitisTarihi })
                .ToListAsync(ct);

            var toplamHastane = await _context.Hospitals.AsNoTracking().CountAsync(ct);
            var overview = new HospitalBirimOverviewVm
            {
                ToplamHastane = toplamHastane,
                AktifEslesmeVarHastane = tumKayitlar.Where(x => x.Aktif).Select(x => x.HospitalID).Distinct().Count(),
                ToplamAktifEslesme = tumKayitlar.Count(x => x.Aktif),
                SuresiYaklasanSayisi = tumKayitlar.Count(x =>
                    x.Aktif && x.BitisTarihi.HasValue
                    && x.BitisTarihi.Value.Date >= bugun
                    && (x.BitisTarihi.Value.Date - bugun).TotalDays <= 30),
                SuresiDolmusAktifSayisi = tumKayitlar.Count(x =>
                    x.Aktif && x.BitisTarihi.HasValue && x.BitisTarihi.Value.Date < bugun)
            };
            ViewBag.Overview = overview;

            if (hospitalId == null)
                return View(null as HospitalBirimPageVm);

            var hospital = await _context.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId.Value, ct);

            if (hospital == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var kayitlar = await _context.HospitalBirimler.AsNoTracking()
                .Include(x => x.Birim)
                .Where(x => x.HospitalID == hospitalId.Value)
                .OrderBy(x => x.Birim.BirimKodu)
                .ThenByDescending(x => x.BaslangicTarihi)
                .ToListAsync(ct);

            var rows = kayitlar.Select(x =>
            {
                int? kalanGun = null;
                bool suresiDoldu = false;
                bool suresiYaklasti = false;

                if (x.BitisTarihi.HasValue)
                {
                    var bitis = x.BitisTarihi.Value.Date;
                    var fark = (bitis - bugun).TotalDays;
                    kalanGun = (int)fark;

                    if (x.Aktif && fark < 0) suresiDoldu = true;
                    else if (x.Aktif && fark <= 30 && fark >= 0) suresiYaklasti = true;
                }

                return new HospitalBirimRowVm
                {
                    Id = x.Id,
                    BirimId = x.BirimId,
                    BirimKodu = x.Birim.BirimKodu,
                    BirimAdi = x.Birim.BirimAdi,
                    Aktif = x.Aktif,
                    BaslangicTarihi = x.BaslangicTarihi,
                    BitisTarihi = x.BitisTarihi,
                    IhaleKayitNo = x.IhaleKayitNo,
                    Aciklama = x.Aciklama,
                    KalanGun = kalanGun,
                    SuresiDoldu = suresiDoldu,
                    SuresiYaklasti = suresiYaklasti
                };
            }).ToList();

            var mevcutBirimIds = kayitlar.Select(x => x.BirimId).Distinct().ToHashSet();
            var eklenebilir = await _context.Birimler.AsNoTracking()
                .Where(b => b.Aktif && !mevcutBirimIds.Contains(b.Id))
                .OrderBy(b => b.BirimKodu)
                .Select(b => new BirimMiniVm
                {
                    Id = b.Id,
                    BirimKodu = b.BirimKodu,
                    BirimAdi = b.BirimAdi
                })
                .ToListAsync(ct);

            var vm = new HospitalBirimPageVm
            {
                HospitalID = hospital.HospitalID,
                HospitalName = hospital.HospitalName,
                AktifKayitlar = rows.Where(r => r.Aktif).ToList(),
                PasifKayitlar = rows.Where(r => !r.Aktif).ToList(),
                EklenebilirBirimler = eklenebilir
            };

            return View(vm);
        }

        // GET: yeni kayıt formu
        [HttpGet]
        public async Task<IActionResult> Create(int hospitalId, int? birimId, CancellationToken ct)
        {
            var hospital = await _context.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId, ct);

            if (hospital == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            await LoadBirimlerAsync(birimId, ct);
            ViewBag.HospitalName = hospital.HospitalName;

            return View(new HospitalBirimFormVm
            {
                HospitalID = hospitalId,
                BirimId = birimId ?? 0,
                Aktif = true,
                BaslangicTarihi = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HospitalBirimFormVm model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                await LoadBirimlerAsync(model.BirimId, ct);
                ViewBag.HospitalName = (await _context.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == model.HospitalID, ct))?.HospitalName ?? "-";
                return View(model);
            }

            // Tarih mantık kontrolü
            if (model.BaslangicTarihi.HasValue && model.BitisTarihi.HasValue
                && model.BitisTarihi < model.BaslangicTarihi)
            {
                ModelState.AddModelError(nameof(model.BitisTarihi),
                    "Bitiş tarihi, başlangıç tarihinden önce olamaz.");
                await LoadBirimlerAsync(model.BirimId, ct);
                ViewBag.HospitalName = (await _context.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == model.HospitalID, ct))?.HospitalName ?? "-";
                return View(model);
            }

            var entity = new HospitalBirim
            {
                HospitalID = model.HospitalID,
                BirimId = model.BirimId,
                Aktif = model.Aktif,
                BaslangicTarihi = model.BaslangicTarihi,
                BitisTarihi = model.BitisTarihi,
                IhaleKayitNo = string.IsNullOrWhiteSpace(model.IhaleKayitNo) ? null : model.IhaleKayitNo.Trim(),
                Aciklama = string.IsNullOrWhiteSpace(model.Aciklama) ? null : model.Aciklama.Trim(),
                OlusturmaTarihi = DateTime.Now
            };

            _context.HospitalBirimler.Add(entity);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = "Hastane-birim kaydı eklendi.";
            return RedirectToAction(nameof(Index), new { hospitalId = model.HospitalID });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var entity = await _context.HospitalBirimler.AsNoTracking()
                .Include(x => x.Hospital)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (entity == null)
            {
                TempData["Error"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            await LoadBirimlerAsync(entity.BirimId, ct);
            ViewBag.HospitalName = entity.Hospital.HospitalName;

            return View(new HospitalBirimFormVm
            {
                Id = entity.Id,
                HospitalID = entity.HospitalID,
                BirimId = entity.BirimId,
                Aktif = entity.Aktif,
                BaslangicTarihi = entity.BaslangicTarihi,
                BitisTarihi = entity.BitisTarihi,
                IhaleKayitNo = entity.IhaleKayitNo,
                Aciklama = entity.Aciklama
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HospitalBirimFormVm model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                await LoadBirimlerAsync(model.BirimId, ct);
                ViewBag.HospitalName = (await _context.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == model.HospitalID, ct))?.HospitalName ?? "-";
                return View(model);
            }

            var entity = await _context.HospitalBirimler.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
            if (entity == null)
            {
                TempData["Error"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (model.BaslangicTarihi.HasValue && model.BitisTarihi.HasValue
                && model.BitisTarihi < model.BaslangicTarihi)
            {
                ModelState.AddModelError(nameof(model.BitisTarihi),
                    "Bitiş tarihi, başlangıç tarihinden önce olamaz.");
                await LoadBirimlerAsync(model.BirimId, ct);
                ViewBag.HospitalName = (await _context.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == model.HospitalID, ct))?.HospitalName ?? "-";
                return View(model);
            }

            entity.BirimId = model.BirimId;
            entity.Aktif = model.Aktif;
            entity.BaslangicTarihi = model.BaslangicTarihi;
            entity.BitisTarihi = model.BitisTarihi;
            entity.IhaleKayitNo = string.IsNullOrWhiteSpace(model.IhaleKayitNo) ? null : model.IhaleKayitNo.Trim();
            entity.Aciklama = string.IsNullOrWhiteSpace(model.Aciklama) ? null : model.Aciklama.Trim();
            entity.GuncellenmeTarihi = DateTime.Now;

            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = "Kayıt güncellendi.";
            return RedirectToAction(nameof(Index), new { hospitalId = model.HospitalID });
        }

        // Hızlı pasife alma (yenilenen ihale senaryosu için)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAktif(int id, CancellationToken ct)
        {
            var entity = await _context.HospitalBirimler.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity == null)
            {
                TempData["Error"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            entity.Aktif = !entity.Aktif;
            entity.GuncellenmeTarihi = DateTime.Now;
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = entity.Aktif
                ? "Kayıt aktif edildi."
                : "Kayıt pasife alındı.";

            return RedirectToAction(nameof(Index), new { hospitalId = entity.HospitalID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _context.HospitalBirimler.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity == null)
            {
                TempData["Error"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            int hospitalId = entity.HospitalID;
            _context.HospitalBirimler.Remove(entity);
            await _context.SaveChangesAsync(ct);

            TempData["SuccessMessage"] = "Kayıt silindi.";
            return RedirectToAction(nameof(Index), new { hospitalId });
        }

        // ──────────────────────────────────────────────────────────
        private async Task LoadBirimlerAsync(int? selectedBirimId, CancellationToken ct)
        {
            ViewBag.Birimler = await _context.Birimler.AsNoTracking()
                .Where(b => b.Aktif)
                .OrderBy(b => b.BirimKodu)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"{b.BirimKodu} – {b.BirimAdi}",
                    Selected = selectedBirimId.HasValue && b.Id == selectedBirimId.Value
                })
                .ToListAsync(ct);
        }

        [HttpGet]
        public async Task<IActionResult> CreatePartial(int hospitalId, int? birimId, CancellationToken ct)
        {
            var hospital = await _context.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId, ct);

            if (hospital == null)
                return NotFound();

            await LoadBirimlerAsync(birimId, ct);
            ViewBag.HospitalName = hospital.HospitalName;

            var model = new HospitalBirimFormVm
            {
                HospitalID = hospitalId,
                BirimId = birimId ?? 0,
                Aktif = true,
                BaslangicTarihi = DateTime.Today
            };

            return PartialView("_CreatePartial", model);
        }

        // ──────────────────────────────────────────────────────────
        // MODAL — JSON döner (AJAX submit)
        // ──────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAjax(HospitalBirimFormVm model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors });
            }

            if (model.BaslangicTarihi.HasValue && model.BitisTarihi.HasValue
                && model.BitisTarihi < model.BaslangicTarihi)
            {
                return Json(new
                {
                    success = false,
                    errors = new Dictionary<string, string[]>
                    {
                        [nameof(model.BitisTarihi)] = new[] { "Bitiş tarihi, başlangıç tarihinden önce olamaz." }
                    }
                });
            }

            try
            {
                var entity = new HospitalBirim
                {
                    HospitalID = model.HospitalID,
                    BirimId = model.BirimId,
                    Aktif = model.Aktif,
                    BaslangicTarihi = model.BaslangicTarihi,
                    BitisTarihi = model.BitisTarihi,
                    IhaleKayitNo = string.IsNullOrWhiteSpace(model.IhaleKayitNo) ? null : model.IhaleKayitNo.Trim(),
                    Aciklama = string.IsNullOrWhiteSpace(model.Aciklama) ? null : model.Aciklama.Trim(),
                    OlusturmaTarihi = DateTime.Now
                };

                _context.HospitalBirimler.Add(entity);
                await _context.SaveChangesAsync(ct);

                return Json(new { success = true, message = "Hastane-birim kaydı eklendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Kayıt sırasında hata: " + ex.Message });
            }
        }
    }
}