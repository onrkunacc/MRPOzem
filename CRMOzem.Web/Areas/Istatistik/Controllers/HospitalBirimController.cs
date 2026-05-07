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

        // /Istatistik/HospitalBirim?hospitalId=1
        [HttpGet]
        public async Task<IActionResult> Index(int? hospitalId, CancellationToken ct)
        {
            await LoadHospitalsAsync(hospitalId, ct);

            if (hospitalId == null)
                return View(null as HospitalBirimPageVm);

            var hospital = await _context.Hospitals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalID == hospitalId.Value, ct);

            if (hospital == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Bu hastaneye ait tüm hastane-birim kayıtları
            var kayitlar = await _context.HospitalBirimler.AsNoTracking()
                .Include(x => x.Birim)
                .Where(x => x.HospitalID == hospitalId.Value)
                .OrderBy(x => x.Birim.BirimKodu)
                .ThenByDescending(x => x.BaslangicTarihi)
                .ToListAsync(ct);

            var rows = kayitlar.Select(x => new HospitalBirimRowVm
            {
                Id = x.Id,
                BirimId = x.BirimId,
                BirimKodu = x.Birim.BirimKodu,
                BirimAdi = x.Birim.BirimAdi,
                Aktif = x.Aktif,
                BaslangicTarihi = x.BaslangicTarihi,
                BitisTarihi = x.BitisTarihi,
                IhaleKayitNo = x.IhaleKayitNo,
                Aciklama = x.Aciklama
            }).ToList();

            // Bu hastane için hiç kaydı olmayan birimler (hızlı ekleme listesi için)
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
    }
}