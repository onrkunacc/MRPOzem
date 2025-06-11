using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    public class FileBrowserController : Controller
    {
        private readonly string _nasRootPath = @"\\192.168.1.200\IHALE";

        public IActionResult Index(string path = "")
        {
            try
            {
                var fullPath = Path.Combine(_nasRootPath, path ?? "");
                if (!Directory.Exists(fullPath))
                    return NotFound("Klasör bulunamadı.");

                var items = Directory.GetFileSystemEntries(fullPath)
                    .Select(p => new FileItemViewModel
                    {
                        Name = Path.GetFileName(p),
                        IsDirectory = Directory.Exists(p),
                        RelativePath = Path.Combine(path ?? "", Path.GetFileName(p))
                    }).ToList();

                var model = new FileBrowserViewModel
                {
                    CurrentPath = path ?? "",
                    Items = items
                };

                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Hata oluştu: {ex.Message}";
                return View(new FileBrowserViewModel());
            }
        }

        public IActionResult Download(string path)
        {
            try
            {
                var fullPath = Path.Combine(_nasRootPath, path);
                if (!System.IO.File.Exists(fullPath))
                    return NotFound("Dosya bulunamadı.");

                var mime = "application/octet-stream"; // MIME tipi dinamikleştirilebilir
                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                return File(stream, mime, Path.GetFileName(fullPath));
            }
            catch (Exception ex)
            {
                return BadRequest($"İndirme hatası: {ex.Message}");
            }
        }

        public IActionResult Preview(string path)
        {
            try
            {
                var fullPath = Path.Combine(_nasRootPath, path);
                if (!System.IO.File.Exists(fullPath))
                    return NotFound("Dosya bulunamadı.");

                var mime = GetMimeType(fullPath);
                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                return File(stream, mime);
            }
            catch (Exception ex)
            {
                return BadRequest($"Önizleme hatası: {ex.Message}");
            }
        }

        private string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
    }
}
