using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik,Aplikasyon")]
    public class HospitalTestSetController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHospitalTestSetService _service;

        public HospitalTestSetController(ApplicationDbContext db, IHospitalTestSetService service)
        {
            _db = db;
            _service = service;
        }

        private async Task LoadHospitalsAsync(int? selectedHospitalId)
        {
            var hospitals = await _db.Hospitals.AsNoTracking()
                .OrderBy(x => x.HospitalName)
                .Select(x => new SelectListItem
                {
                    Value = x.HospitalID.ToString(),
                    Text = x.HospitalName
                })
                .ToListAsync();

            ViewBag.Hospitals = hospitals;
            ViewBag.SelectedHospitalId = selectedHospitalId;
        }

        // /Istatistik/HospitalTestSet?hospitalId=1
        public async Task<IActionResult> Index(int? hospitalId)
        {
            await LoadHospitalsAsync(hospitalId);

            if (hospitalId == null)
                return View(null);

            var vm = await _service.GetAsync(hospitalId.Value);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyStandardSet(int hospitalId)
        {
            await _service.ApplyStandardSetAsync(hospitalId);
            return RedirectToAction(nameof(Index), new { hospitalId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAll(int hospitalId, bool isActive)
        {
            await _service.SetAllActiveAsync(hospitalId, isActive);
            return RedirectToAction(nameof(Index), new { hospitalId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(HospitalTestSetSaveVm model)
        {

            if (model?.Rows != null)
            {
                foreach (var r in model.Rows)
                    Console.WriteLine($"  -> TestId={r.TestId}, IsActive={r.IsActive}");
            }

            try
            {
                if (model == null || model.Rows == null || model.Rows.Count == 0)
                {
                    TempData["Error"] = "Kaydedilecek veri bulunamadı.";
                    return RedirectToAction(nameof(Index), new { hospitalId = model?.HospitalId });
                }

                var dict = model.Rows
                    .GroupBy(r => r.TestId)
                    .ToDictionary(g => g.Key, g => g.First().IsActive);

                Console.WriteLine($"Dict sayısı: {dict.Count}");
                foreach (var kv in dict)
                    Console.WriteLine($"  Dict: TestId={kv.Key}, IsActive={kv.Value}");

                await _service.UpdateActiveFlagsAsync(model.HospitalId, dict);

                Console.WriteLine($"==== SAVE END ====");

                TempData["SuccessMessage"] = "Test seti güncellendi.";
                return RedirectToAction(nameof(Index), new { hospitalId = model.HospitalId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: {ex.Message}");
                TempData["Error"] = "Kayıt sırasında hata: " + ex.Message;
                return RedirectToAction(nameof(Index), new { hospitalId = model?.HospitalId });
            }
        }
    }
}
