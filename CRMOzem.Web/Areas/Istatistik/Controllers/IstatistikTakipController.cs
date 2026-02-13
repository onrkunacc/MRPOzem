using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Istatistik;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "İstatistik,Yönetici")]
    public class IstatistikTakipController : Controller
    {
        private readonly IIstatistikService _istatistikService;
        private readonly IKurumService _kurumService;

        public IstatistikTakipController(IIstatistikService istatistikService, IKurumService kurumService)
        {
            _istatistikService = istatistikService;
            _kurumService = kurumService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? yil, int[] aylar)
        {
            var y = yil ?? DateTime.Now.Year;
            var model = await _istatistikService.GetDashboardAsync(y, aylar ?? Array.Empty<int>());
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle([FromBody] SetGeldiMiRequest req)
        {
            await _istatistikService.SetGeldiMiAsync(req);
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNote([FromBody] SetNotRequest req)
        {
            await _istatistikService.SetNotAsync(req);
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHospital(AddKurumRequest req)
        {
            var id = await _kurumService.AddAsync(req);
            return Json(new { ok = true, id });
        }
    }
}
