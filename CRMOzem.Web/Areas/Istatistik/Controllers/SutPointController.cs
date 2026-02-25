using CRMOzem.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    public class SutPointController : Controller
    {
        private readonly ISutPointService _service;

        public SutPointController(ISutPointService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? year)
        {
            int y = year ?? DateTime.Now.Year;
            var vm = await _service.GetAsync(y);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int year, int[] testId, string[] point)
        {
            var dict = new Dictionary<int, decimal?>();

            for (int i = 0; i < testId.Length; i++)
            {
                decimal? p = null;
                var raw = i < point.Length ? point[i] : null;

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // TR format destek: 10,08 gibi
                    if (decimal.TryParse(raw, out var val) || decimal.TryParse(raw.Replace(".", ","), out val))
                        p = val;
                }

                dict[testId[i]] = p;
            }

            await _service.SaveAsync(year, dict);
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyFrom(int fromYear, int toYear, bool overwrite = false)
        {
            await _service.CopyFromYearAsync(fromYear, toYear, overwrite);
            return RedirectToAction(nameof(Index), new { year = toYear });
        }
    }
}
