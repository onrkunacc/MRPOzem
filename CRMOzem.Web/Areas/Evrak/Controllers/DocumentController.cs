using CRMOzem.Core.Models;
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
        public async Task<IActionResult> Index()
        {
            var documents = await _context.FollowDocuments
                .Include(d => d.Category) // Kategoriyi de dahil et
                .ToListAsync();
            return View(documents);
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
