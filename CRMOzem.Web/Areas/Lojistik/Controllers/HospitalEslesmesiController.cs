using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Lojistik.Controllers
{
    [Area("Lojistik")]
    [Authorize(Roles = "Yönetici,KotaTakip,Lojistik")]
    public class HospitalEslesmesiController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ISiparisSyncService _sync;

        public HospitalEslesmesiController(ApplicationDbContext db, ISiparisSyncService sync)
        {
            _db = db;
            _sync = sync;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var mevcutEslesmeler = await _db.HospitalEslesmeleri
                .AsNoTracking()
                .Include(e => e.InternalHospital)
                .OrderBy(e => e.InternalHospital!.HospitalName)
                .ToListAsync(ct);

            var sonuc = await _sync.SyncAsync(ct);

            ViewBag.MevcutEslesmeler = mevcutEslesmeler;
            ViewBag.EslesmeyenMusteriler = sonuc.EslesmeyenMusteriler;
            ViewBag.SyncSonucOzet = sonuc.Ozet;

            ViewBag.Hospitals = await _db.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(
            string musteriKodu,
            string? kurumAdi,
            int? hospitalId,
            bool yoksay,
            string? not,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(musteriKodu))
            {
                TempData["Error"] = "Müşteri kodu zorunlu.";
                return RedirectToAction(nameof(Index));
            }

            var kod = musteriKodu.Trim().ToUpperInvariant();

            var mevcut = await _db.HospitalEslesmeleri
                .FirstOrDefaultAsync(e => e.ExternalMusteriKodu == kod, ct);

            if (mevcut != null)
            {
                mevcut.InternalHospitalID = yoksay ? null : hospitalId;
                mevcut.Yoksay = yoksay;
                mevcut.ExternalKurumAdi = kurumAdi;
                mevcut.Not = not;
                mevcut.GuncellemeTarihi = DateTime.Now;
                TempData["SuccessMessage"] = $"'{kod}' güncellendi.";
            }
            else
            {
                if (!yoksay && !hospitalId.HasValue)
                {
                    TempData["Error"] = "Hastane seç ya da 'Yoksay' işaretle.";
                    return RedirectToAction(nameof(Index));
                }

                _db.HospitalEslesmeleri.Add(new HospitalEslesmesi
                {
                    ExternalMusteriKodu = kod,
                    ExternalKurumAdi = kurumAdi,
                    InternalHospitalID = yoksay ? null : hospitalId,
                    Yoksay = yoksay,
                    Not = not,
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = User.Identity?.Name
                });
                TempData["SuccessMessage"] = $"'{kod}' eşleştirildi.";
            }

            await _db.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id, CancellationToken ct)
        {
            var e = await _db.HospitalEslesmeleri.FindAsync(new object[] { id }, ct);
            if (e != null)
            {
                _db.HospitalEslesmeleri.Remove(e);
                await _db.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = "Eşleşme silindi.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════════════
        //  ★ YENİ: Hızlı hastane ekleme (AJAX)
        // ═══════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHospitalAjax(
            [FromForm] string hospitalName,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(hospitalName))
                return Json(new { success = false, message = "Hastane adı zorunlu." });

            var ad = hospitalName.Trim();

            // Zaten var mı? (case-insensitive)
            var mevcut = await _db.Hospitals
                .FirstOrDefaultAsync(h => h.HospitalName.ToLower() == ad.ToLower(), ct);

            if (mevcut != null)
            {
                return Json(new
                {
                    success = true,
                    hospitalId = mevcut.HospitalID,
                    hospitalName = mevcut.HospitalName,
                    alreadyExists = true,
                    message = "Bu adda hastane zaten var — mevcut olanı seçildi."
                });
            }

            var yeni = new Hospital
            {
                HospitalName = ad,
                Aktif = true
            };
            _db.Hospitals.Add(yeni);
            await _db.SaveChangesAsync(ct);

            return Json(new
            {
                success = true,
                hospitalId = yeni.HospitalID,
                hospitalName = yeni.HospitalName,
                alreadyExists = false,
                message = "Hastane eklendi."
            });
        }


    }
}
