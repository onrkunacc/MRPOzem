using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using iTextSharp.text;
using iTextSharp.text.pdf;
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
