using CRMOzem.Core.Interfaces.Inventory;
using CRMOzem.Core.Interfaces.Storage;
using CRMOzem.DataAccess.Context;
using CRMOzem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace CRMOzem.Web.Areas.IT.Controllers
{
    [Authorize(Roles = "IT")]
    public sealed class AssignmentsController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IAssignmentPdfService _pdf;
        private readonly IFileStorage _storage;
        private readonly ILogger<AssignmentsController> _log;

        public AssignmentsController(ApplicationDbContext db, IAssignmentPdfService pdf, IFileStorage storage, ILogger<AssignmentsController> log)
        {
            _db = db;
            _pdf = pdf;
            _storage = storage;
            _log = log;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ReturnItem(int itemId, string? conditionIn, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var item = await _db.AssignmentItems.Include(x => x.Asset).FirstOrDefaultAsync(x => x.Id == itemId, ct);
                if (item is null) return NotFound();

                if (item.ReturnedAtUtc is not null)
                    return BadRequest(new { ok = false, message = "Zaten iade alınmış." });

                item.ReturnedAtUtc = DateTime.UtcNow;
                item.ConditionIn = conditionIn;

                if (item.Asset is not null)
                    item.Asset.Status = Core.Enums.AssetStatus.Available;

                await _db.SaveChangesAsync(ct);
                return Ok(new { ok = true });
            }, _log);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> GeneratePdf(int assignmentId, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var (fileKey, fileName) = await _pdf.CreateAndStoreAsync(assignmentId, ct);

                _db.Documents.Add(new Core.Inventory.Document
                {
                    AssignmentId = assignmentId,
                    FileKey = fileKey,
                    FileName = fileName,
                    ContentType = "application/pdf"
                });

                await _db.SaveChangesAsync(ct);
                return Ok(new { ok = true });
            }, _log);

        [HttpGet]
        public Task<IActionResult> DownloadDoc(int docId, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == docId, ct);
                if (doc is null) return NotFound();

                var (stream, contentType, fileName) = await _storage.OpenAsync(doc.FileKey, ct);
                return File(stream, contentType, fileName);
            }, _log);
    }
}
