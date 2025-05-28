using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{

    public class FileTransferWebController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        public FileTransferWebController(IWebHostEnvironment env, ApplicationDbContext context)
        {
            _env = env;
            _context = context;
        }

        public IActionResult Index()
        {
            var model = new FileTransferPageViewModel
            {
                UploadedFiles = _context.FileTransferQueues
                    .OrderByDescending(f => f.Id)
                    .Take(20)
                    .ToList()
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Upload(FileTransferPageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.UploadedFiles = _context.FileTransferQueues
                    .OrderByDescending(f => f.Id)
                    .Take(20)
                    .ToList();
                return View("Index", model);
            }

            var file = model.UploadModel.File;
            var targetPath = model.UploadModel.TargetPath;

            var tempPath = Path.Combine(_env.WebRootPath, "uploads", "temp");
            Directory.CreateDirectory(tempPath);

            var filePath = Path.Combine(tempPath, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _context.FileTransferQueues.Add(new FileTransferQueue
            {
                FileName = file.FileName,
                FileUrl = filePath,
                TargetPath = targetPath,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Dosya başarıyla yüklendi.";
            return RedirectToAction("Index");
        }
    }
}
