using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Evrak.Controllers
{
    [Area("Evrak")]
    [Authorize(Roles ="Yönetici,Evrak,Muhasebe,Finans")]
    public class DocumentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DocumentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Evrak Listesi
        [HttpGet]
        public async Task<IActionResult> Index(string searchTerm, int page = 1)
        {
            int pageSize = 20;

            var query = _context.FollowDocuments
                .Include(d => d.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(d => d.Subject.Contains(searchTerm) ||
                                         d.Code.Contains(searchTerm));
            }

            var totalDocs = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalDocs / (double)pageSize);

            var documents = await query
                .OrderByDescending(d => d.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new DocumentListViewModel
            {
                Documents = documents,
                SearchTerm = searchTerm,
                Page = page,
                TotalPages = totalPages
            };

            return View(viewModel);
        }
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.DocumentCategories
                    .Select(c => new { c.Id, c.Description })
                    .ToListAsync();

                return Json(categories); 
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Hata oluştu", Details = ex.Message });
            }
        }

        public IActionResult AddDocument()
        {
            return View(new FollowDocument());
        }

        [HttpPost]
        public async Task<IActionResult> AddDocument([FromForm] FollowDocument document)
        {

            if (document.CategoryId == 0)
            {
                ModelState.AddModelError("CategoryId", "Lütfen bir kategori seçin.");
            }
            if (string.IsNullOrWhiteSpace(document.Subject))
            {
                ModelState.AddModelError("Subject", "Lütfen bir konu girin.");
            }

            ModelState.Remove("Code");
            ModelState.Remove("Category");

            try
            {
                var category = await _context.DocumentCategories.FirstOrDefaultAsync(c => c.Id == document.CategoryId);
                if (category == null)
                {
                    ModelState.AddModelError("", "Seçilen kategori geçersiz.");
                    return View(document);
                }

                var currentYear = DateTime.Now.Year;


                int nextId = (await _context.FollowDocuments.MaxAsync(d => (int?)d.Id) ?? 0) + 1;
                document.Code = $"{category.Code}.{currentYear}.{nextId}";
                document.Date = DateTime.Now;

                _context.FollowDocuments.Add(document);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
                ModelState.AddModelError("", "Beklenmeyen bir hata oluştu.");
                return View(document);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditDocument(int id)
        {
            var document = await _context.FollowDocuments.FindAsync(id);
            if (document == null)
                return NotFound();

            var model = new EditDocumentViewModel
            {
                Id = document.Id,
                CategoryId = document.CategoryId,
                Subject = document.Subject,
                DeliveryMethod = document.DeliveryMethod,
                CargoCompany = document.CargoCompany
            };

            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> EditDocument(EditDocumentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Hataları logla veya debug et
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    foreach (var error in state.Errors)
                    {
                        Console.WriteLine($"Validation Hatası [{key}]: {error.ErrorMessage}");
                    }
                }

                return View(model);
            }

            try
            {
                var document = await _context.FollowDocuments.FindAsync(model.Id);
                if (document == null)
                    return NotFound("Evrak bulunamadı.");

                document.Subject = model.Subject;
                document.DeliveryMethod = model.DeliveryMethod;
                document.CargoCompany = model.DeliveryMethod == "Kargo" ? model.CargoCompany : null;
                document.CategoryId = model.CategoryId;

                _context.FollowDocuments.Update(document);
                await _context.SaveChangesAsync();

                TempData["DocumentMessage"] = "Evrak başarıyla güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Beklenmeyen bir hata oluştu: " + ex.Message);
                return View(model);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var document = await _context.FollowDocuments.FindAsync(id);
                if (document == null)
                {
                    return NotFound(new { success = false, message = "Evrak bulunamadı." });
                }

                // ❗ Not varsa silme uyarısı (opsiyonel - frontendde de yapılabilir)
                if (!string.IsNullOrWhiteSpace(document.Notes))
                {
                    // İstersen bu kontrolü frontend'e taşıyabiliriz
                    return BadRequest(new { success = false, message = "Notu bulunan evrak silinemez. Önce notu temizleyin." });
                }

                _context.FollowDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Evrak başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Sunucu hatası: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsSent(int id)
        {
            var document = await _context.FollowDocuments.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            document.IsSent = true;
            document.SentDate = DateTime.Now;

            _context.FollowDocuments.Update(document);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evrak gönderildi olarak işaretlendi." });
        }

        [HttpPost]
        public async Task<IActionResult> AddNote(int id, [FromBody] Dictionary<string, string> data)
        {
            var document = await _context.FollowDocuments.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            if (!data.ContainsKey("note"))
            {
                return BadRequest(new { message = "Geçersiz veri!" });
            }

            document.Notes = data["note"];

            _context.FollowDocuments.Update(document);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Not başarıyla eklendi.", notes = document.Notes });
        }
    }
}
