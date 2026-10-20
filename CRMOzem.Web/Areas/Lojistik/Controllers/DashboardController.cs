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
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILojistikDashboardService _service;
        private readonly ISaglikRaporuService _saglik;
        private readonly IFizikiSayimSyncService _sayimSync;

        public DashboardController(
            ApplicationDbContext db,
            ILojistikDashboardService service,
            ISaglikRaporuService saglik,
            IFizikiSayimSyncService sayimSync)
        {
            _db = db;
            _service = service;
            _saglik = saglik;
            _sayimSync = sayimSync;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var vm = await _service.GetDashboardAsync(ct);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SaglikRaporu(CancellationToken ct)
        {
            var vm = await _saglik.UretAsync(ct);
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  Sayım Sync — crm.ozemmedikal.com'dan tüm aktif sayımları çek
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SayimlariYenile(CancellationToken ct)
        {
            var sonuc = await _sayimSync.SyncAsync(hospitalId: null, ct);

            if (sonuc.Basarili)
            {
                var mesaj = $"Sayım sync başarılı · Çekilen: {sonuc.CekilenSatir} satır · " +
                            $"Yeni: {sonuc.YeniEklenen} · Güncellenen: {sonuc.Guncellenen} · " +
                            $"Reaktif eşleşen: {sonuc.ReaktifEslesen} · Reaktif eşleşmeyen: {sonuc.ReaktifEslesemeyen}";

                if (sonuc.EslesemeyenHastaneler.Any())
                    mesaj += $" · ⚠ {sonuc.EslesemeyenHastaneler.Count} hastane adı eşleşmedi";

                TempData["SuccessMessage"] = mesaj;
            }
            else
            {
                TempData["Error"] = $"Sayım sync HATASI: {sonuc.HataMesaji}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════════
        //  Sayım Eşleşme sayfası
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> SayimEslesme(CancellationToken ct)
        {
            var rapor = await _sayimSync.TestEslesmeAsync(ct);

            // Dropdown için CRMOzem hastaneleri
            ViewBag.Hospitals = await _db.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            return View(rapor);
        }

        // ═══════════════════════════════════════════════════════════
        //  ★ YENİ: Manuel eşleşme kaydet (AJAX)
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SayimEslesmeKaydet(
            [FromForm] string apiHospitalName,
            [FromForm] int? internalHospitalId,
            [FromForm] bool yoksay,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiHospitalName))
                return Json(new { success = false, message = "API hastane adı boş olamaz." });

            if (!yoksay && !internalHospitalId.HasValue)
                return Json(new { success = false, message = "Hastane seç veya 'Yoksay' işaretle." });

            var ad = apiHospitalName.Trim();

            var mevcut = await _db.FizikiSayimHastaneEslesmeleri
                .FirstOrDefaultAsync(e => e.ApiHospitalName == ad, ct);

            if (mevcut != null)
            {
                mevcut.InternalHospitalID = yoksay ? null : internalHospitalId;
                mevcut.Yoksay = yoksay;
                mevcut.GuncellemeTarihi = DateTime.Now;
            }
            else
            {
                _db.FizikiSayimHastaneEslesmeleri.Add(new FizikiSayimHastaneEslesmesi
                {
                    ApiHospitalName = ad,
                    InternalHospitalID = yoksay ? null : internalHospitalId,
                    Yoksay = yoksay,
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = User.Identity?.Name
                });
            }

            await _db.SaveChangesAsync(ct);
            return Json(new { success = true, message = yoksay ? "Yoksay işaretlendi." : "Eşleştirme kaydedildi." });
        }

        // ═══════════════════════════════════════════════════════════
        //  ★ YENİ: Hızlı hastane ekleme (AJAX) — SayimEslesme için
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HastaneHizliEkle(
            [FromForm] string hospitalName,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(hospitalName))
                return Json(new { success = false, message = "Hastane adı zorunlu." });

            var ad = hospitalName.Trim();

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

            var yeni = new Hospital { HospitalName = ad, Aktif = true };
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
