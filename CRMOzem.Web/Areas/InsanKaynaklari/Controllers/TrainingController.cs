using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.InsanKaynaklari.Controllers
{
    [Area("InsanKaynaklari")]
    [Authorize(Roles = "İnsan Kaynakları,Yönetici")]
    public class TrainingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TrainingController> _logger;

        public TrainingController(ApplicationDbContext context, ILogger<TrainingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult List()
        {
            try
            {
                var list = _context.Trainings
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new HrTrainingItemViewModel
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        TrainingDate = t.TrainingDate,
                        IsPublished = t.IsPublished,
                        Link = t.Link
                    }).ToList();

                var vm = new HrTrainingListViewModel { Trainings = list };
                return View(vm);
            }
            catch (Exception ex)
            {
                return View(new HrTrainingListViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrainingCreateViewModel vm, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Geçersiz veri. Lütfen tüm alanları doldurun.";
                    return RedirectToAction(nameof(List));
                }

                var entity = new Training
                {
                    Title = vm.Title,
                    Description = vm.Description,
                    TrainingDate = vm.TrainingDate,
                    Link = vm.Link,
                    IsPublished = vm.IsPublished,
                    CreatedBy = User.Identity?.Name ?? "hr",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Trainings.Add(entity);
                await _context.SaveChangesAsync(ct);

                TempData["Success"] = "Eğitim başarıyla oluşturuldu.";
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/Create POST hatası");
                TempData["Error"] = "Eğitim oluşturulamadı.";
                return RedirectToAction(nameof(List));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TrainingCreateViewModel vm, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Geçersiz veri. Lütfen kontrol edin.";
                    return RedirectToAction(nameof(List));
                }

                var t = _context.Trainings.FirstOrDefault(x => x.Id == id);
                if (t == null)
                {
                    TempData["Error"] = "Eğitim bulunamadı.";
                    return RedirectToAction(nameof(List));
                }

                t.Title = vm.Title;
                t.Description = vm.Description;
                t.TrainingDate = vm.TrainingDate;
                t.Link = vm.Link;
                t.IsPublished = vm.IsPublished;

                _context.Trainings.Update(t);
                await _context.SaveChangesAsync(ct);

                TempData["Success"] = "Eğitim güncellendi.";
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/Edit POST hatası");
                TempData["Error"] = "Eğitim güncellenemedi.";
                return RedirectToAction(nameof(List));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                var t = _context.Trainings.FirstOrDefault(x => x.Id == id);
                if (t == null)
                {
                    TempData["Error"] = "Eğitim bulunamadı.";
                    return RedirectToAction(nameof(List));
                }

                _context.Trainings.Remove(t);
                await _context.SaveChangesAsync(ct);
                TempData["Success"] = "Eğitim silindi.";
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/Delete hatası");
                TempData["Error"] = "Eğitim silinemedi.";
                return RedirectToAction(nameof(List));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id, CancellationToken ct)
        {
            try
            {
                var t = _context.Trainings.FirstOrDefault(x => x.Id == id);
                if (t == null)
                {
                    TempData["Error"] = "Eğitim bulunamadı.";
                    return RedirectToAction(nameof(List));
                }

                t.IsPublished = !t.IsPublished;
                _context.Trainings.Update(t);
                await _context.SaveChangesAsync(ct);

                TempData["Success"] = t.IsPublished ? "Eğitim yayınlandı." : "Eğitim yayından kaldırıldı.";
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/TogglePublish hatası");
                TempData["Error"] = "Yayın durumu değiştirilemedi.";
                return RedirectToAction(nameof(List));
            }
        }
    }
}
