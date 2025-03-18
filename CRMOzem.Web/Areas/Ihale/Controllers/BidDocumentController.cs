using System.Globalization;
using System.Text;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Ihale.Controllers
{
    [Area("Ihale")]
    [Authorize(Roles = "Yönetici,İhale")]
    public class BidDocumentController : Controller
    {
        private readonly IAwsS3FileService _fileService;

        public BidDocumentController(IAwsS3FileService fileService)
        {
            _fileService = fileService;
        }

        public async Task<IActionResult> Index(string folderName = "")
        {
            folderName ??= "";

            var files = await _fileService.ListFilesAsync(folderName);  // Sadece bu klasörün dosyalarını al
            var folders = await _fileService.ListFoldersAsync(folderName);

            ViewBag.CurrentFolder = folderName;
            ViewBag.Folders = folders;
            ViewBag.Breadcrumbs = folderName.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

            return View(files);  // Dosyalar bu klasördeyse listelenecek
        }



        public async Task<IActionResult> CreateFolder(string parentFolder, string folderName)
        {
            var normalizedFolderName = NormalizeFolderName(folderName);
            await _fileService.CreateFolderAsync(parentFolder, normalizedFolderName);
            return RedirectToAction("Index", new { folderName = parentFolder });
        }
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Dosya seçilmedi veya geçersiz.";
                return RedirectToAction("Index", new { folderName });
            }

            try
            {
                await using var stream = file.OpenReadStream();

                // folderName zaten tam path olduğu için yeniden normalize ETME!
                await _fileService.UploadFileAsync(stream, file.FileName, folderName);

                TempData["Success"] = "Dosya başarıyla yüklendi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Dosya yükleme hatası: {ex.Message}";
            }

            return RedirectToAction("Index", new { folderName });
        }


        [HttpPost]
        public async Task<IActionResult> Delete(string fileName, string folderName)
        {
            try
            {
                await _fileService.DeleteFileAsync(fileName, folderName);
                TempData["Success"] = "Dosya başarıyla silindi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Dosya silme hatası: {ex.Message}";
            }

            return RedirectToAction("Index", new { folderName });
        }


        private string GetParentFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return null;
            var parts = folderPath.Split('/');
            return parts.Length > 1 ? string.Join('/', parts.Take(parts.Length - 1)) : "";
        }
        public string NormalizeFolderName(string name)
        {
            return string.Concat(name.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
                .Replace("ı", "i")
                .Replace("İ", "I")
                .Replace("ç", "c")
                .Replace("Ç", "C")
                .Replace("ğ", "g")
                .Replace("Ğ", "G")
                .Replace("ş", "s")
                .Replace("Ş", "S")
                .Replace("ö", "o")
                .Replace("Ö", "O")
                .Replace("ü", "u")
                .Replace("Ü", "U");
        }
    }
}
