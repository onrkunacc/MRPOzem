using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Istatistik;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "İstatistik,Yönetici")]
    public class HospitalTakipController : Controller
    {
        private readonly IHospitalIstatistikService _service;

        public HospitalTakipController(IHospitalIstatistikService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? yil, int[] aylar)
        {
            try
            {
                var y = yil ?? DateTime.Now.Year;
                var model = await _service.GetDashboardAsync(y, aylar ?? Array.Empty<int>());
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HospitalIstatistikDashboardVm
                {
                    Yil = yil ?? DateTime.Now.Year,
                    SeciliAylar = Enumerable.Range(1, 12).ToList()
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle([FromBody] SetHospitalGeldiMiRequest req)
        {
            try
            {
                await _service.SetGeldiMiAsync(req);
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNote([FromBody] SetHospitalNotRequest req)
        {
            try
            {
                await _service.SetNotAsync(req);
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message });
            }
        }
    }
}