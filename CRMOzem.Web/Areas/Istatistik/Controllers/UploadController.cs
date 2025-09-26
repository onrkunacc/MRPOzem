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
    public class UploadController : Controller
    {
        private readonly IFileParserService _fileParser;
        private readonly IStatisticsService _statisticsService;
        private readonly ApplicationDbContext _context;

        public UploadController(IFileParserService fileParser, IStatisticsService statisticsService,ApplicationDbContext context)
        {
            _fileParser = fileParser;
            _statisticsService = statisticsService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Hospitals = new SelectList(_context.Hospitals, "HospitalID", "HospitalName");
            return View(new UploadStatisticsViewModel
            {
                Month = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1) 
            });

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UploadStatisticsViewModel model)
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
                    model.PreviewItems = await _fileParser.ParseExcelAsync(model.File);
                }
                else if (model.File.FileName.EndsWith(".pdf"))
                {
                    model.PreviewItems = await _fileParser.ParsePdfAsync(model.File);
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
                TempData["Error"] = $"Hata: {ex.Message}";
                return View(model);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int hospitalId, DateTime month, List<StatisticsPreviewItem> items)
        {
            try
            {
                if (items == null || !items.Any())
                {
                    TempData["Error"] = "Kaydedilecek veri bulunamadı.";
                    return RedirectToAction("Index");
                }

                foreach (var item in items.Where(x => !x.HasError))
                {
                    await _statisticsService.AddUsageAsync(
                        hospitalId,
                        item.TestID ?? 0,
                        month,
                        item.TestCount
                    );
                }

                TempData["SuccessMessage"] = "Dosyadaki geçerli veriler başarıyla kaydedildi.";
                return RedirectToAction("Index", "Usage");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Kayıt sırasında hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
