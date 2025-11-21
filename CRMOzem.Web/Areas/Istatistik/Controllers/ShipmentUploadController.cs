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
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
            return View(new ShipmentUploadViewModel { Month = DateTime.Now });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ShipmentUploadViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                return View(model);
            }

            try
            {
                if (model.File == null || model.File.Length == 0)
                {
                    TempData["Error"] = "Lütfen bir dosya seçiniz.";
                    return RedirectToAction(nameof(Index));
                }

                var fileName = model.File.FileName;
                var ext = Path.GetExtension(fileName).ToLowerInvariant();

                if (ext == ".xlsx")
                {
                    model.PreviewItems = await _fileParser.ParseShipmentExcelAsync(model.File);
                }
                else if (ext == ".pdf")
                {
                    model.PreviewItems = await _fileParser.ParseShipmentPdfAsync(model.File);
                }
                else
                {
                    TempData["ErrorMessage"] = "Sadece Excel (.xlsx) veya PDF (.pdf) dosyaları yükleyebilirsiniz.";
                    return RedirectToAction(nameof(Index));
                }

                if (!model.PreviewItems.Any())
                {
                    TempData["Warning"] = "Dosyadan veri okunamadı.";
                }

                ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName", model.HospitalID);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Yükleme sırasında hata: {ex.Message}";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int hospitalId, DateTime month, List<ShipmentPreviewItem> items, bool confirmSave, CancellationToken ct)
        {
            try
            {
                if (!confirmSave)
                {
                    TempData["ErrorMessage"] = "Kaydetmeden önce onay kutusunu işaretleyin.";
                    return RedirectToAction(nameof(Index));
                }

                if (items == null || !items.Any())
                {
                    TempData["ErrorMessage"] = "Kaydedilecek veri bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var item in items.Where(x => !x.HasError))
                {
                    await _statisticsService.AddShipmentAsync(
                        hospitalId,
                        item.TestID ?? 0,
                        month,
                        item.SentCount,
                        ct
                    );
                }

                TempData["SuccessMessage"] = "Gönderim verileri başarıyla kaydedildi.";
                return RedirectToAction("Index", "Shipment");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Kayıt sırasında hata: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
