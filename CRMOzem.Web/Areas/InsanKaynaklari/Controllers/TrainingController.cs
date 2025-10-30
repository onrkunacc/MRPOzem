using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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
                var trainings = _context.Trainings
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new HrTrainingItemViewModel
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        TrainingDate = t.TrainingDate,
                        IsPublished = t.IsPublished,
                        Link = t.Link
                    })
                    .ToList();

                return View(new HrTrainingListViewModel
                {
                    Trainings = trainings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/List hatası");
                TempData["Error"] = "Eğitim listesi yüklenemedi.";
                return View(new HrTrainingListViewModel());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                var userRole = _context.Roles.FirstOrDefault(r => r.Name == "Kullanıcı");
                if (userRole == null)
                    return BadRequest("Kullanıcı rolü bulunamadı.");

                var userIds = _context.UserRoles
                    .Where(ur => ur.RoleId == userRole.Id)
                    .Select(ur => ur.UserId)
                    .ToList();

                var users = _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName, u.UserName })
                    .AsEnumerable()
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = $"{u.FullName} ({u.UserName})"
                    })
                    .OrderBy(u => u.Text)
                    .ToList();

                var vm = new TrainingCreateViewModel
                {
                    AvailableUsers = users,
                    TrainingDate = DateTime.Now
                };

                return PartialView("~/Areas/InsanKaynaklari/Views/Training/_CreateTrainingPartial.cshtml", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/Create GET hatası");
                return BadRequest("Eğitim oluşturma ekranı yüklenemedi.");
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
                    TempData["Error"] = "Lütfen gerekli alanları doldurun.";
                    return RedirectToAction(nameof(Create));
                }

                if (string.IsNullOrWhiteSpace(vm.Title) || string.IsNullOrWhiteSpace(vm.Link))
                {
                    TempData["Error"] = "Eğitim başlığı ve link zorunludur.";
                    return RedirectToAction(nameof(Create));
                }

                var training = new Training
                {
                    Title = vm.Title.Trim(),
                    Description = vm.Description?.Trim(),
                    TrainingDate = vm.TrainingDate,
                    Link = vm.Link.Trim(),
                    IsPublished = vm.IsPublished,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "hr"
                };

                _context.Trainings.Add(training);
                await _context.SaveChangesAsync(ct);

                // 🔹 Eğitim soruları kaydet
                if (vm.Questions != null && vm.Questions.Any())
                {
                    var questions = vm.Questions.Select(q => new TrainingQuestion
                    {
                        TrainingId = training.Id,
                        QuestionText = q.QuestionText,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectOption = q.CorrectOption
                    }).ToList();

                    _context.TrainingQuestions.AddRange(questions);
                }

                // Kullanıcı atamaları
                var userIds = vm.SelectedUserIds;
                if (userIds != null && userIds.Any())
                {
                    var visibleUsers = userIds.Select(uid => new TrainingVisibleUser
                    {
                        TrainingId = training.Id,
                        UserId = uid
                    }).ToList();

                    _context.TrainingVisibleUsers.AddRange(visibleUsers);
                    _logger.LogInformation($"Eğitim {training.Id} için {visibleUsers.Count} kullanıcı atandı.");
                }
                else
                {
                    _logger.LogWarning($"Eğitim {training.Id} için hiçbir kullanıcı seçilmedi.");
                }

                await _context.SaveChangesAsync(ct);

                TempData["Success"] = "Eğitim ve sınav başarıyla oluşturuldu.";
                return RedirectToAction("List");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/Create POST hatası");
                TempData["Error"] = "Eğitim oluşturulurken hata oluştu.";
                return RedirectToAction(nameof(Create));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TrainingCreateViewModel vm, CancellationToken ct)
        {
            try
            {
                var t = _context.Trainings.FirstOrDefault(x => x.Id == id);
                if (t == null)
                {
                    TempData["Error"] = "Eğitim bulunamadı.";
                    return RedirectToAction(nameof(List));
                }

                t.Title = vm.Title?.Trim();
                t.Description = vm.Description?.Trim();
                t.TrainingDate = vm.TrainingDate;
                t.Link = vm.Link?.Trim();
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

        [HttpGet]
        public IActionResult Report(int? trainingId)
        {
            try
            {
                var trainings = _context.Trainings
                    .OrderByDescending(t => t.TrainingDate)
                    .Select(t => new HrTrainingItemViewModel
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        TrainingDate = t.TrainingDate,
                        IsPublished = t.IsPublished,
                        Link = t.Link
                    })
                    .ToList();

                var vm = new HrTrainingReportViewModel
                {
                    AvailableTrainings = trainings,
                    SelectedTrainingId = trainingId
                };

                if (trainingId.HasValue)
                {
                    // 🔹 Sadece "Kullanıcı" rolüne sahip olanlar
                    var roleId = _context.Roles.FirstOrDefault(r => r.Name == "Kullanıcı")?.Id;
                    if (roleId == null)
                    {
                        TempData["Error"] = "Kullanıcı rolü bulunamadı.";
                        return View(vm);
                    }

                    var userIdsInRole = _context.UserRoles
                        .Where(ur => ur.RoleId == roleId)
                        .Select(ur => ur.UserId)
                        .ToList();

                    var allUsers = _context.Users
                        .Where(u => userIdsInRole.Contains(u.Id))
                        .Select(u => new { u.Id, u.FullName, u.UserName })
                        .ToList();

                    // 🔹 Eğitim katılımları
                    var participations = _context.TrainingParticipations
                        .Where(p => p.TrainingId == trainingId.Value)
                        .ToList();

                    // 🔹 Eşleşme: TrainingParticipations.UserId = AspNetUsers.UserName
                    vm.Participations = allUsers.Select(u =>
                    {
                        var p = participations
                            .FirstOrDefault(x => x.UserId.Equals(u.UserName, StringComparison.OrdinalIgnoreCase));

                        return new HrTrainingParticipationViewModel
                        {
                            UserName = u.FullName,
                            HasParticipated = p != null,
                            IsCompleted = p?.IsCompleted ?? false,
                            JoinedAt = p?.JoinedAt,
                            CompletedAt = p?.CompletedAt
                        };
                    })
                    .OrderBy(x => x.UserName)
                    .ToList();
                }

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HR/Report hatası");
                TempData["Error"] = "Rapor yüklenemedi.";
                return RedirectToAction(nameof(List));
            }
        }


        [HttpGet]
        public IActionResult DownloadReportPdf(int trainingId)
        {
            var training = _context.Trainings.FirstOrDefault(x => x.Id == trainingId);
            if (training == null)
                return NotFound();

            var participations = _context.TrainingParticipations
                .Where(x => x.TrainingId == trainingId)
                .ToList();

            using (var memoryStream = new MemoryStream())
            {
                // Türkçe karakter desteği (Arial Unicode)
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font normal = new Font(bf, 11, Font.NORMAL, BaseColor.BLACK);
                Font header = new Font(bf, 13, Font.BOLD, new BaseColor(0, 70, 130));

                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter.GetInstance(doc, memoryStream);
                doc.Open();

                doc.Add(new Paragraph($"Eğitim Raporu - {training.Title}", header));
                doc.Add(new Paragraph($"Tarih: {training.TrainingDate:dd.MM.yyyy}", normal));
                doc.Add(new Paragraph(" "));

                PdfPTable table = new PdfPTable(4);
                table.WidthPercentage = 100;
                table.AddCell(new PdfPCell(new Phrase("Kullanıcı", header)));
                table.AddCell(new PdfPCell(new Phrase("Katılma Tarihi", header)));
                table.AddCell(new PdfPCell(new Phrase("Durum", header)));
                table.AddCell(new PdfPCell(new Phrase("Tamamlama Tarihi", header)));

                foreach (var p in participations)
                {
                    var user = _context.Users.FirstOrDefault(u => u.UserName == p.UserId);
                    string fullName = user?.FullName ?? p.UserId;

                    table.AddCell(new Phrase(fullName, normal));
                    table.AddCell(new Phrase(p.JoinedAt.ToString("dd.MM.yyyy HH:mm") ?? "-", normal));
                    table.AddCell(new Phrase(p.IsCompleted ? "Tamamlandı" : "Devam Ediyor", normal));
                    table.AddCell(new Phrase(p.CompletedAt?.ToString("dd.MM.yyyy HH:mm") ?? "-", normal));
                }

                doc.Add(table);
                doc.Close();

                return File(memoryStream.ToArray(), "application/pdf", $"EgitimRaporu_{training.Title}.pdf");
            }
        }
    }
}
