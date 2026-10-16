
using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Lojistik.Controllers
{
    [Area("Lojistik")]
    [Authorize(Roles = "Yönetici,KotaTakip,Lojistik")]
    public class StokOneriController : Controller
    {
        private readonly IStokHesaplamaService _hesap;
        private readonly ApplicationDbContext _db;

        public StokOneriController(IStokHesaplamaService hesap, ApplicationDbContext db)
        {
            _hesap = hesap;
            _db = db;
        }

        // ═══════════════════════════════════════════════════════════
        //  Hastane seçim ekranı
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var hospitals = await _db.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new
                {
                    h.HospitalID,
                    h.HospitalName,
                    SnapshotSayisi = _db.FizikiSayimSnapshotlari.Count(s =>
                        s.HospitalID == h.HospitalID
                        && s.SayimAktifMi
                        && s.UrunTipi == CRMOzem.Core.Enums.UrunTipi.Reaktif
                        && s.MappedTestID.HasValue),
                    SonSayim = _db.FizikiSayimSnapshotlari
                        .Where(s => s.HospitalID == h.HospitalID && s.SayimAktifMi)
                        .Max(s => (DateTime?)s.SayimTarihi)
                })
                .ToListAsync(ct);

            ViewBag.Hospitals = hospitals;
            return View();
        }

        // ═══════════════════════════════════════════════════════════
        //  Hastane Detay — ana ekran
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> HastaneDetay(int id, int runwayAy = 3, CancellationToken ct = default)
        {
            var panel = await _hesap.HesaplaHastaneAsync(id, runwayAy, ct);
            if (panel == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            return View(panel);
        }
    }
}