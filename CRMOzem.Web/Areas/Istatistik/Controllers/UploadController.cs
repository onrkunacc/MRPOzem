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
    public class UploadController : Controller
    {
        private readonly IFileParserService _fileParser;
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public UploadController(IFileParserService fileParser, IStatisticsService statisticsService, ApplicationDbContext context)
        {
            _fileParser = fileParser;
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Hospitals = new SelectList(await _context.Hospitals.AsNoTracking().OrderBy(x => x.HospitalName).ToListAsync(ct), "HospitalID", "HospitalName");
            return View(new UploadStatisticsViewModel
            {
                Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UploadStatisticsViewModel model, CancellationToken ct)
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
                    TempData["ErrorMessage"] = "Lütfen bir dosya seçiniz.";
                    return RedirectToAction(nameof(Index));
                }

                var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();

                if (ext == ".xlsx")
                {
                    model.PreviewItems = await _fileParser.ParseExcelAsync(model.File);
                }
                else if (ext == ".pdf")
                {
                    model.PreviewItems = await _fileParser.ParsePdfAsync(model.File);
                }
                else
                {
                    TempData["Error"] = "Sadece Excel (.xlsx) veya PDF (.pdf) dosyaları yükleyebilirsiniz.";
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
                TempData["Error"] = $"Hata: {ex.Message}";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int hospitalId, DateTime month, List<StatisticsPreviewItem> items, CancellationToken ct)
        {
            try
            {
                if (items == null || !items.Any())
                {
                    TempData["Error"] = "Kaydedilecek veri bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var item in items.Where(x => !x.HasError))
                {
                    await _statisticsService.AddUsageAsync(
                        hospitalId,
                        item.TestID ?? 0,
                        month,
                        item.TestCount,
                        ct
                    );
                }

                TempData["Success"] = "Dosyadaki geçerli veriler başarıyla kaydedildi.";
                return RedirectToAction("Index", "Usage");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Kayıt sırasında hata: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
