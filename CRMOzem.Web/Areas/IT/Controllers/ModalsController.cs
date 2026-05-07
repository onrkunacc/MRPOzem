using CRMOzem.Core.Enums;
using CRMOzem.Core.Inventory;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using CRMOzem.Web.Controllers;
using CRMOzem.Web.Vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.IT.Controllers
{
    [Area("IT")]
    [Authorize(Roles = "IT")]
    public sealed class ModalsController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ModalsController> _log;

        public ModalsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<ModalsController> log)
        {
            _db = db;
            _userManager = userManager;
            _log = log;
        }

        // ─────────────────────────────────────────────
        //  Cihaz
        // ─────────────────────────────────────────────
        [HttpGet]
        public Task<IActionResult> AssetCreate() =>
            SafeExecuteAsync(() => Task.FromResult<IActionResult>(
                PartialView("_AssetCreate", new AssetCreateVm())), _log);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> AssetCreate(AssetCreateVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { ok = false, message = "Eksik/hatalı alan var." });

                // Aynı seri no ile kayıt var mı?
                if (!string.IsNullOrWhiteSpace(vm.SerialNumber))
                {
                    var sn = vm.SerialNumber.Trim();
                    if (await _db.Assets.AsNoTracking().AnyAsync(a => a.SerialNumber == sn, ct))
                        return BadRequest(new { ok = false, message = "Bu seri numarasıyla kayıtlı bir cihaz zaten var." });
                }

                _db.Assets.Add(new Asset
                {
                    Category = vm.Category.Trim(),
                    Brand = vm.Brand.Trim(),
                    Model = vm.Model.Trim(),
                    SerialNumber = vm.SerialNumber?.Trim(),
                    InventoryTag = vm.InventoryTag?.Trim(),
                    ImeiNo = vm.ImeiNo?.Trim(),
                    BarcodeNo = vm.BarcodeNo?.Trim(),
                    Status = AssetStatus.Available
                });

                await _db.SaveChangesAsync(ct);
                return Ok(new { ok = true });
            }, _log);

        // ─────────────────────────────────────────────
        //  Kayıtsız Kişi
        // ─────────────────────────────────────────────
        [HttpGet]
        public Task<IActionResult> AssigneeExternalCreate() =>
            SafeExecuteAsync(() => Task.FromResult<IActionResult>(
                PartialView("_AssigneeExternalCreate", new AssigneeExternalVm())), _log);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> AssigneeExternalCreate(AssigneeExternalVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { ok = false, message = "Eksik/hatalı alan var." });

                var a = new Assignee
                {
                    FullName = vm.FullName.Trim(),
                    Title = vm.Title,
                    Department = vm.Department,
                    TcNo = vm.TcNo,
                    Phone = vm.Phone,
                    Email = vm.Email
                };

                _db.Assignees.Add(a);
                await _db.SaveChangesAsync(ct);
                return Ok(new { ok = true, assigneeId = a.Id, name = a.FullName });
            }, _log);

        // ─────────────────────────────────────────────
        //  Kayıtlı Kullanıcıdan Kişi
        // ─────────────────────────────────────────────
        [HttpGet]
        public Task<IActionResult> AssigneeFromUser() =>
            SafeExecuteAsync(async () =>
            {
                var users = await _db.Users.AsNoTracking()
                    .OrderBy(x => x.FullName).Take(500).ToListAsync();
                return PartialView("_AssigneeFromUser", new AssigneeFromUserVm { Users = users });
            }, _log);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> AssigneeFromUser(AssigneeFromUserVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(vm.UserId))
                    return BadRequest(new { ok = false, message = "Kullanıcı seç." });

                var user = await _userManager.FindByIdAsync(vm.UserId);
                if (user is null)
                    return BadRequest(new { ok = false, message = "Kullanıcı bulunamadı." });

                // Zaten var mı?
                var existing = await _db.Assignees
                    .FirstOrDefaultAsync(x => x.UserId == user.Id, ct);
                if (existing is not null)
                    return Ok(new { ok = true, assigneeId = existing.Id, name = existing.FullName });

                var a = new Assignee
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Title = user.Title,
                    Phone = user.PhoneNumber,
                    Email = user.Email
                };

                _db.Assignees.Add(a);
                await _db.SaveChangesAsync(ct);
                return Ok(new { ok = true, assigneeId = a.Id, name = a.FullName });
            }, _log);

        // ─────────────────────────────────────────────
        //  Zimmet  ← Çift kayıt buradan geliyordu
        // ─────────────────────────────────────────────
        [HttpGet]
        public Task<IActionResult> AssignmentCreate(CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var vm = new AssignmentCreateVm
                {
                    AvailableAssets = await _db.Assets.AsNoTracking()
                        .Where(x => x.Status == AssetStatus.Available)
                        .OrderBy(x => x.Brand).ThenBy(x => x.Model)
                        .ToListAsync(ct),
                    RecentAssignees = await _db.Assignees.AsNoTracking()
                        .OrderByDescending(x => x.Id).Take(50)
                        .ToListAsync(ct)
                };
                return PartialView("_AssignmentCreate", vm);
            }, _log);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> AssignmentCreate(AssignmentCreatePostVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (vm.AssigneeId <= 0 || vm.AssetIds == null || vm.AssetIds.Count == 0)
                    return BadRequest(new { ok = false, message = "Kişi ve en az 1 cihaz seçmelisiniz." });

                // Cihazları tracking ile çek
                var assets = await _db.Assets
                    .Where(x => vm.AssetIds.Contains(x.Id))
                    .ToListAsync(ct);

                if (assets.Count != vm.AssetIds.Count)
                    return BadRequest(new { ok = false, message = "Seçilen cihazlardan biri bulunamadı." });

                if (assets.Any(x => x.Status != AssetStatus.Available))
                    return BadRequest(new { ok = false, message = "Seçilen cihazlardan biri müsait değil." });

                // ── DÜZELTİLDİ ──────────────────────────────────────────────
                //  Eski kod: assignment.Items.Add(...) ile navigation property'ye ekliyor,
                //  ardından _db.Assignments.Add(assignment) diyordu.
                //  EF Core bu durumda Items'ı navigation üzerinden de track edebiliyor
                //  ve bazı konfigürasyonlarda çift INSERT atıyor.
                //
                //  Yeni yaklaşım:
                //   1. Sadece Assignment kaydet  → veritabanından gerçek Id gelsin
                //   2. Items'ı AddRange ile ayrı ekle  → navigation property hiç dokunulmuyor
                // ────────────────────────────────────────────────────────────

                var assignment = new Assignment
                {
                    AssigneeId = vm.AssigneeId,
                    Notes = vm.Notes
                    // Items = boş bırak, aşağıda AddRange ile eklenecek
                };

                _db.Assignments.Add(assignment);
                await _db.SaveChangesAsync(ct);   // Id üretildi

                var items = assets.Select(a => new AssignmentItem
                {
                    AssignmentId = assignment.Id,
                    AssetId = a.Id,
                    ConditionOut = vm.ConditionOut
                }).ToList();

                foreach (var a in assets)
                    a.Status = AssetStatus.InUse;

                _db.AssignmentItems.AddRange(items);
                await _db.SaveChangesAsync(ct);

                return Ok(new { ok = true, assignmentId = assignment.Id });
            }, _log);
    }
}
