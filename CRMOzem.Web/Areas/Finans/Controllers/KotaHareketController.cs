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
    public class KotaHareketController : Controller
    {
        private readonly IKotaHareketService _service;
        private readonly ApplicationDbContext _db;
        private readonly ITcmbKurService _kurService;
        private readonly UserManager<ApplicationUser> _userManager;

        public KotaHareketController(
            IKotaHareketService service,
            ApplicationDbContext db,
            ITcmbKurService kurService,
            UserManager<ApplicationUser> userManager)
        {
            _service = service;
            _db = db;
            _kurService = kurService;
            _userManager = userManager;
        }

        // /Finans/KotaHareket/Detay/5


        [HttpGet]
        public async Task<IActionResult> Detay(int kotaId, CancellationToken ct)
        {
            var kota = await _db.Kotalar
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.KotaID == kotaId, ct);

            if (kota == null)
            {
                TempData["Error"] = "Kota bulunamadı.";
                return RedirectToAction("Index", "Kota");
            }

            var hareketler = await _db.KotaHareketleri
                .AsNoTracking()
                .Include(h => h.Tedarikci)
                .Include(h => h.Hospital)
                .Where(h => h.KotaID == kotaId)
                .OrderByDescending(h => h.FaturaTarihi)
                .ToListAsync(ct);

            var vm = new KotaHareketDetayVm
            {
                Kota = new KotaInfoVm
                {
                    KotaID = kota.KotaID,
                    KotaKodu = kota.KotaKodu,
                    IsAdi = kota.IsAdi,
                    Sorumlu = kota.Sorumlu,
                    Bolge = kota.Bolge,
                    BaslamaTarihi = kota.BaslamaTarihi,
                    BitisTarihi = kota.BitisTarihi,
                    ProformaTutarUsd = kota.ProformaTutarUsd ?? 0m,
                    Durum = kota.Durum
                },
                Hareketler = hareketler.Select(h => new KotaHareketItemVm
                {
                    ID = h.KotaHareketID,          // ← PK adını kontrol et
                    Tarih = h.FaturaTarihi ?? DateTime.MinValue,
                    Tur = h.HareketTuru,
                    TedarikciAdi = h.Tedarikci?.Ad,          // ✓ doğru
                    TedarikciGrup = h.Tedarikci?.Grup,
                    HospitalID = h.HospitalID,
                    HospitalName = h.Hospital?.HospitalName, // ✓ doğru
                    Aciklama = h.Aciklama,
                    TutarUsd = h.UsdKarsiligi,
                    TutarTl = h.Tutar                   // ← TL alanının adı neyse
                }).ToList()
            };

            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> Create(int kotaId, KotaHareketTuru? hareketTuru, CancellationToken ct)
        {
            var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(k => k.KotaID == kotaId, ct);
            if (kota == null)
            {
                TempData["Error"] = "Kota bulunamadı.";
                return RedirectToAction("Index", "Kota");
            }

            await LoadFormDataAsync(kotaId);
            ViewBag.KotaAdi = kota.IsAdi;
            ViewBag.KotaKodu = kota.KotaKodu;

            var vm = new KotaHareketCreateVm
            {
                KotaID = kotaId,
                FaturaTarihi = DateTime.Today,
                ParaBirimi = "USD",
                HareketTuru = hareketTuru.HasValue && Enum.IsDefined(typeof(KotaHareketTuru), hareketTuru.Value)
                    ? hareketTuru.Value : KotaHareketTuru.Gider
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KotaHareketCreateVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync(vm.KotaID);
                var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(k => k.KotaID == vm.KotaID, ct);
                ViewBag.KotaAdi = kota?.IsAdi;
                ViewBag.KotaKodu = kota?.KotaKodu;
                return View(vm);
            }

            try
            {
                var userId = _userManager.GetUserId(User);
                await _service.CreateAsync(vm, userId, ct);
                TempData["SuccessMessage"] = "Fatura eklendi.";
                return RedirectToAction(nameof(Detay), new { id = vm.KotaID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
                await LoadFormDataAsync(vm.KotaID);
                var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(k => k.KotaID == vm.KotaID, ct);
                ViewBag.KotaAdi = kota?.IsAdi;
                ViewBag.KotaKodu = kota?.KotaKodu;
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var vm = await _service.GetHareketAsync(id, ct);
            if (vm == null)
            {
                TempData["Error"] = "Hareket bulunamadı.";
                return RedirectToAction("Index", "Kota");
            }

            await LoadFormDataAsync(vm.KotaID);
            var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(k => k.KotaID == vm.KotaID, ct);
            ViewBag.KotaAdi = kota?.IsAdi;
            ViewBag.KotaKodu = kota?.KotaKodu;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(KotaHareketEditVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync(vm.KotaID);
                var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(k => k.KotaID == vm.KotaID, ct);
                ViewBag.KotaAdi = kota?.IsAdi;
                ViewBag.KotaKodu = kota?.KotaKodu;
                return View(vm);
            }

            try
            {
                await _service.UpdateAsync(vm, ct);
                TempData["SuccessMessage"] = "Fatura güncellendi.";
                return RedirectToAction(nameof(Detay), new { id = vm.KotaID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
                await LoadFormDataAsync(vm.KotaID);
                var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(k => k.KotaID == vm.KotaID, ct);
                ViewBag.KotaAdi = kota?.IsAdi;
                ViewBag.KotaKodu = kota?.KotaKodu;
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int kotaId, CancellationToken ct)
        {
            try
            {
                await _service.DeleteAsync(id, ct);
                TempData["SuccessMessage"] = "Fatura silindi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Silme hatası: " + ex.Message;
            }
            return RedirectToAction(nameof(Detay), new { id = kotaId });
        }

        // ─────────────────────────────────────────
        // AJAX: TCMB kur bilgisi
        // GET /Finans/KotaHareket/GetKur?tarih=2026-09-15
        // ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetKur(DateTime? tarih, CancellationToken ct)
        {
            try
            {
                var kur = await _kurService.GetKurAsync(tarih ?? DateTime.Today, ct);
                if (kur == null)
                {
                    return Json(new KurBilgiVm
                    {
                        Basarili = false,
                        Mesaj = "TCMB'den kur alınamadı. Manuel girmelisiniz."
                    });
                }
                return Json(new KurBilgiVm
                {
                    Basarili = true,
                    Kur = kur.UsdSatis,
                    KurTarihi = kur.Tarih,
                    Mesaj = kur.ManuelGirildi ? "Manuel giriş" : "TCMB verisi"
                });
            }
            catch (Exception ex)
            {
                return Json(new KurBilgiVm
                {
                    Basarili = false,
                    Mesaj = "Hata: " + ex.Message
                });
            }
        }

        private async Task LoadFormDataAsync(int kotaId)
        {
            // Tedarikçiler (mevcut)
            ViewBag.Tedarikciler = await _db.Tedarikciler.AsNoTracking()
                .Where(t => t.Aktif)
                .OrderBy(t => t.Grup).ThenBy(t => t.Sira).ThenBy(t => t.Ad)
                .Select(t => new
                {
                    t.TedarikciID,
                    t.Ad,
                    Grup = t.Grup.ToString()
                })
                .ToListAsync();

            // ═══ YENİ: Bu kotaya bağlı hastaneler ═══
            ViewBag.Hospitals = await _db.Set<KotaHospital>().AsNoTracking()
                .Where(kh => kh.KotaID == kotaId)
                .Include(kh => kh.Hospital)
                .OrderBy(kh => kh.Hospital!.HospitalName)
                .Select(kh => new
                {
                    kh.HospitalID,
                    HospitalName = kh.Hospital!.HospitalName
                })
                .ToListAsync();
        }
    }
}