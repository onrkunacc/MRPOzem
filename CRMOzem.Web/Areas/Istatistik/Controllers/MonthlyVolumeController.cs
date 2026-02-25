using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    public class MonthlyVolumeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IMonthlyVolumeService _service;

        public MonthlyVolumeController(ApplicationDbContext db, IMonthlyVolumeService service)
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

        // /Istatistik/MonthlyVolume?hospitalId=1&year=2026&month=2
        public async Task<IActionResult> Index(int? hospitalId, int? year, int? month)
        {
            await LoadHospitalsAsync(hospitalId);

            if (hospitalId == null)
                return View(null);

            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;

            var vm = await _service.GetEntryAsync(hospitalId.Value, y, m);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyPrev(int hospitalId, int year, int month, bool overwriteExisting = false)
        {
            await _service.CopyPreviousMonthAsync(hospitalId, year, month, overwriteExisting);
            return RedirectToAction(nameof(Index), new { hospitalId, year, month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int hospitalId, int year, int month, int[] testId, int[] count)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < testId.Length; i++)
                dict[testId[i]] = i < count.Length ? count[i] : 0;

            await _service.SaveAsync(hospitalId, year, month, dict);
            return RedirectToAction(nameof(Index), new { hospitalId, year, month });
        }
    }
}
