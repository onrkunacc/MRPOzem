using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileTransferController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FileTransferController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 1. Dosya Yükleme
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string targetPath)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Geçerli bir dosya yüklenmedi.");

            var tempPath = Path.Combine(_env.WebRootPath, "uploads", "temp");
            Directory.CreateDirectory(tempPath);

            var filePath = Path.Combine(tempPath, file.FileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var record = new FileTransferQueue
            {
                FileName = file.FileName,
                FileUrl = filePath,
                TargetPath = targetPath
            };

            _context.FileTransferQueues.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Dosya başarıyla yüklendi", record.Id });
        }

        // 2. Bekleyen Dosyalar
        [HttpGet("pending")]
        public IActionResult GetPendingFiles()
        {
            var files = _context.FileTransferQueues.Where(f => !f.IsTransferred).ToList();
            return Ok(files);
        }

        // 3. Dosyayı Görüntüle
        [HttpGet("view/{id}")]
        public IActionResult ViewFile(int id)
        {
            var file = _context.FileTransferQueues.FirstOrDefault(f => f.Id == id);
            if (file == null) return NotFound("Kayıt bulunamadı.");
            if (!System.IO.File.Exists(file.FileUrl)) return NotFound("Dosya fiziksel olarak yok.");

            var stream = new FileStream(file.FileUrl, FileMode.Open, FileAccess.Read);
            return File(stream, "application/octet-stream", file.FileName);
        }

        // 4. Transfer Bildirimi
        [HttpPost("mark-as-transferred/{id}")]
        public async Task<IActionResult> MarkAsTransferred(int id)
        {
            var record = await _context.FileTransferQueues.FindAsync(id);
            if (record == null) return NotFound();

            record.IsTransferred = true;
            record.TransferredAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Aktarım tamamlandı olarak işaretlendi." });
        }
    }
}
