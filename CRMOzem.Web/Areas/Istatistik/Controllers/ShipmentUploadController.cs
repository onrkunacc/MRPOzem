using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "Yönetici,İstatistik")]
    public class ShipmentUploadController : Controller
    {
        private readonly IFileParserService _fileParser;
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public ShipmentUploadController(IFileParserService fileParser, IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _fileParser = fileParser;
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
            return View(new ShipmentUploadViewModel { Month = DateTime.Now });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ShipmentUploadViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                return View(model);
            }

            try
            {
                if (model.File.FileName.EndsWith(".xlsx"))
                {
                    model.PreviewItems = await _fileParser.ParseShipmentExcelAsync(model.File);
                }
                else if (model.File.FileName.EndsWith(".pdf"))
                {
                    model.PreviewItems = await _fileParser.ParseShipmentPdfAsync(model.File);
                }
                else
                {
                    TempData["Error"] = "Sadece Excel veya PDF dosyaları yükleyebilirsiniz.";
                    return RedirectToAction("Index");
                }

                if (!model.PreviewItems.Any())
                {
                    TempData["Warning"] = "Dosyadan veri okunamadı.";
                }

                ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName", model.HospitalID);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Yükleme sırasında hata: {ex.Message}";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int hospitalId, DateTime month, List<ShipmentPreviewItem> items, bool confirmSave)
        {
            try
            {
                if (!confirmSave)
                {
                    TempData["Error"] = "Kaydetmeden önce onay kutusunu işaretleyin.";
                    return RedirectToAction("Index");
                }

                if (items == null || !items.Any())
                {
                    TempData["Error"] = "Kaydedilecek veri bulunamadı.";
                    return RedirectToAction("Index");
                }

                foreach (var item in items.Where(x => !x.HasError))
                {
                    await _statisticsService.AddShipmentAsync(
                        hospitalId,
                        item.TestID ?? 0,
                        month,
                        item.SentCount
                    );
                }

                TempData["SuccessMessage"] = "Gönderim verileri başarıyla kaydedildi.";
                return RedirectToAction("Index", "Shipment");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Kayıt sırasında hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
