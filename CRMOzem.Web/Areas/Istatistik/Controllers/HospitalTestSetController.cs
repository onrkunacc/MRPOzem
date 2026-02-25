using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
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
        public async Task<IActionResult> Save(int hospitalId, int[] testId, bool[] isActive)
        {
            // testId[] ve isActive[] aynı sırada gelecek şekilde view’de name’leri ayarlayacağız
            var dict = new Dictionary<int, bool>();
            for (int i = 0; i < testId.Length; i++)
                dict[testId[i]] = isActive[i];

            await _service.UpdateActiveFlagsAsync(hospitalId, dict);
            return RedirectToAction(nameof(Index), new { hospitalId });
        }
    }
}
