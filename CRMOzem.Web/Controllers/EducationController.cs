using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    public class EducationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EducationController> _logger;

        public EducationController(ApplicationDbContext context, ILogger<EducationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                var userId = User.Identity?.Name ?? string.Empty;
                var trainings = _context.Trainings
                .Where(t => t.IsPublished)
                .OrderByDescending(t => t.TrainingDate)
                .ToList();


                var participations = _context.TrainingParticipations
                .Where(p => p.UserId == userId)
                .ToList();


                var vm = new UserTrainingListViewModel
                {
                    Items = trainings.Select(t =>
                    {
                        var p = participations.FirstOrDefault(x => x.TrainingId == t.Id);
                        return new UserTrainingListItemViewModel
                        {
                            Id = t.Id,
                            Title = t.Title,
                            TrainingDate = t.TrainingDate,
                            IsJoined = p != null,
                            IsCompleted = p?.IsCompleted ?? false
                        };
                    }).ToList()
                };


                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User/Index hatası");
                TempData["Error"] = "Eğitimler yüklenemedi.";
                return View(new UserTrainingListViewModel());
            }
        }
        //detaylar
        [HttpGet]
        public IActionResult Details(int id)
        {
            try
            {
                var t = _context.Trainings.FirstOrDefault(x => x.Id == id && x.IsPublished);
                if (t == null) return NotFound();


                var userId = User.Identity?.Name ?? string.Empty;
                var p = _context.TrainingParticipations.FirstOrDefault(x => x.TrainingId == id && x.UserId == userId);


                var vm = new UserTrainingDetailViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    TrainingDate = t.TrainingDate,
                    Link = t.Link,
                    IsPublished = t.IsPublished,
                    IsJoined = p != null,
                    JoinedAt = p?.JoinedAt,
                    IsCompleted = p?.IsCompleted ?? false,
                    CompletedAt = p?.CompletedAt
                };


                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User/Details hatası");
                TempData["Error"] = "Detay yüklenemedi.";
                return RedirectToAction(nameof(Index));
            }
        }
        //eğitime katıl
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int id, CancellationToken ct)
        {
            try
            {
                var t = _context.Trainings.FirstOrDefault(x => x.Id == id && x.IsPublished);
                if (t == null) return NotFound();


                var userId = User.Identity?.Name ?? string.Empty;
                var p = _context.TrainingParticipations.FirstOrDefault(x => x.TrainingId == id && x.UserId == userId);


                if (p == null)
                {
                    var newParticipation = new TrainingParticipation
                    {
                        TrainingId = id,
                        UserId = userId,
                        JoinedAt = DateTime.UtcNow,
                        IsCompleted = false
                    };
                    _context.TrainingParticipations.Add(newParticipation);
                    await _context.SaveChangesAsync(ct);
                }


                return Redirect(t.Link); // Google Drive linkine yönlendirme
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User/Join hatası");
                TempData["Error"] = "Eğitime katılım sırasında hata oluştu.";
                return RedirectToAction(nameof(Index));
            }
        }
        //eğitim tamamlandı.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, CancellationToken ct)
        {
            try
            {
                var userId = User.Identity?.Name ?? string.Empty;
                var p = _context.TrainingParticipations.FirstOrDefault(x => x.TrainingId == id && x.UserId == userId);
                if (p == null)
                {
                    TempData["Error"] = "Katılım bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }


                p.IsCompleted = true;
                p.CompletedAt = DateTime.UtcNow;


                _context.TrainingParticipations.Update(p);
                await _context.SaveChangesAsync(ct);


                TempData["Success"] = "Eğitim tamamlandı.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User/Complete hatası");
                TempData["Error"] = "Tamamlama işlemi başarısız.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
