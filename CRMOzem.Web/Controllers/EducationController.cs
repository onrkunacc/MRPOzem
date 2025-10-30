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
                // Giriş yapan kullanıcıyı al
                var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity!.Name);
                if (user == null)
                {
                    TempData["Error"] = "Kullanıcı bilgisi alınamadı.";
                    return View(new UserTrainingListViewModel());
                }

                var userId = user.Id;

                // Kullanıcıya özel eğitimler (TrainingVisibleUsers tablosuna göre)
                var assignedTrainingIds = _context.TrainingVisibleUsers
                    .Where(v => v.UserId == userId)
                    .Select(v => v.TrainingId)
                    .ToList();

                // Sadece atanmış ve yayınlanmış eğitimleri getir
                var trainings = _context.Trainings
                    .Where(t => assignedTrainingIds.Contains(t.Id) && t.IsPublished)
                    .OrderByDescending(t => t.TrainingDate)
                    .ToList();

                // Kullanıcının katılım kayıtlarını al
                var participations = _context.TrainingParticipations
                    .Where(p => p.UserId == user.UserName)
                    .ToList();

                // ViewModel
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
                var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity!.Name);
                if (user == null) return Unauthorized();

                var userId = user.Id;

                // Kullanıcıya atanmış mı kontrol et
                bool hasAccess = _context.TrainingVisibleUsers
                    .Any(v => v.TrainingId == id && v.UserId == userId);

                if (!hasAccess)
                    return Forbid(); // Erişim yetkisi yoksa 403

                var t = _context.Trainings.FirstOrDefault(x => x.Id == id && x.IsPublished);
                if (t == null) return NotFound();

                var p = _context.TrainingParticipations
                    .FirstOrDefault(x => x.TrainingId == id && x.UserId == user.UserName);

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
        [HttpGet]
        public IActionResult Join(int id)
        {
            var training = _context.Trainings.FirstOrDefault(t => t.Id == id && t.IsPublished);
            if (training == null)
                return NotFound();

            return Redirect(training.Link);
        }
        //eğitime katıl
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int id, CancellationToken ct)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity!.Name);
                if (user == null) return Unauthorized();

                var userId = user.Id;

                // Kullanıcıya atanmış mı kontrol et
                bool hasAccess = _context.TrainingVisibleUsers
                    .Any(v => v.TrainingId == id && v.UserId == userId);

                if (!hasAccess)
                    return Forbid();

                var t = _context.Trainings.FirstOrDefault(x => x.Id == id && x.IsPublished);
                if (t == null) return NotFound();

                var p = _context.TrainingParticipations.FirstOrDefault(x => x.TrainingId == id && x.UserId == user.UserName);
                if (p == null)
                {
                    var newParticipation = new TrainingParticipation
                    {
                        TrainingId = id,
                        UserId = user.UserName, // dikkat, Username olarak tutuluyor
                        JoinedAt = DateTime.UtcNow,
                        IsCompleted = false
                    };

                    _context.TrainingParticipations.Add(newParticipation);
                    await _context.SaveChangesAsync(ct);
                }

                return Redirect(t.Link);
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
                var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity!.Name);
                if (user == null) return Unauthorized();

                var userId = user.Id;

                // Kullanıcıya atanmış mı kontrol et
                bool hasAccess = _context.TrainingVisibleUsers
                    .Any(v => v.TrainingId == id && v.UserId == userId);

                if (!hasAccess)
                    return Forbid();

                var p = _context.TrainingParticipations.FirstOrDefault(x => x.TrainingId == id && x.UserId == user.UserName);
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

        [HttpGet]
        public IActionResult GetQuestions(int id)
        {
            try
            {
                var questions = _context.TrainingQuestions
                    .Where(q => q.TrainingId == id)
                    .Select(q => new
                    {
                        q.Id,
                        q.QuestionText,
                        q.OptionA,
                        q.OptionB,
                        q.OptionC,
                        q.OptionD
                    })
                    .ToList();

                return Json(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Education/GetQuestions hatası");
                return StatusCode(500, "Sorular yüklenemedi.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAnswers([FromBody] List<TrainingAnswer> answers)
        {
            try
            {
                if (answers == null || !answers.Any())
                    return BadRequest("Cevaplar boş olamaz.");

                var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity!.Name);
                if (user == null)
                    return Unauthorized();

                var userId = user.UserName;
                var trainingId = answers.First().TrainingId;

                // Soru tabanlı doğru cevapları önceden al
                var correctAnswers = _context.TrainingQuestions
                    .Where(q => q.TrainingId == trainingId)
                    .Select(q => new { q.Id, q.CorrectOption })
                    .ToList();

                var answerEntities = new List<TrainingAnswer>();

                foreach (var ans in answers)
                {
                    var correct = correctAnswers.FirstOrDefault(x => x.Id == ans.QuestionId);
                    bool isCorrect = correct != null && correct.CorrectOption.Equals(ans.SelectedOption, StringComparison.OrdinalIgnoreCase);

                    var entity = new TrainingAnswer
                    {
                        TrainingId = trainingId,
                        QuestionId = ans.QuestionId,
                        UserId = userId,
                        SelectedOption = ans.SelectedOption,
                        IsCorrect = isCorrect
                    };

                    answerEntities.Add(entity);
                }

                _context.TrainingAnswers.AddRange(answerEntities);
                await _context.SaveChangesAsync();

                // Kullanıcının katılımını "tamamlandı" yap
                var participation = _context.TrainingParticipations
                    .FirstOrDefault(p => p.TrainingId == trainingId && p.UserId == userId);

                if (participation != null)
                {
                    participation.IsCompleted = true;
                    participation.CompletedAt = DateTime.UtcNow;
                    _context.TrainingParticipations.Update(participation);
                    await _context.SaveChangesAsync();
                }

                // Sonuç istatistiğini hesapla (doğru/yanlış sayısı)
                int correctCount = answerEntities.Count(a => a.IsCorrect);
                int total = answerEntities.Count;

                return Ok(new
                {
                    success = true,
                    correctCount,
                    total,
                    message = $"Sınav tamamlandı! {correctCount}/{total} doğru yaptınız 🎯"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Education/SubmitAnswers hatası");
                return StatusCode(500, "Cevaplar kaydedilemedi.");
            }
        }
    }
}
