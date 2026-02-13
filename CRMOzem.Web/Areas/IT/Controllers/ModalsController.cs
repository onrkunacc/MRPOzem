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
using System;

namespace CRMOzem.Web.Areas.IT.Controllers
{
    [Area("IT")]
    [Authorize(Roles = "IT")]
    public sealed class ModalsController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ModalsController> _log;

        public ModalsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<ModalsController> log)
        {
            _db = db;
            _userManager = userManager;
            _log = log;
        }

        
        [HttpGet]
        public Task<IActionResult> AssetCreate() =>
            SafeExecuteAsync(() => Task.FromResult<IActionResult>(PartialView("_AssetCreate", new AssetCreateVm())), _log);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AssetCreate(AssetCreateVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (!ModelState.IsValid) return BadRequest(new { ok = false, message = "Eksik/hatalı alan var." });

                _db.Assets.Add(new Asset
                {
                    Category = vm.Category,
                    Brand = vm.Brand,
                    Model = vm.Model,
                    SerialNumber = vm.SerialNumber,
                    InventoryTag = vm.InventoryTag,
                    ImeiNo = vm.ImeiNo,
                    BarcodeNo = vm.BarcodeNo,
                    Status = AssetStatus.Available
                });

                await _db.SaveChangesAsync(ct);
                return Ok(new { ok = true });
            }, _log);

        [HttpGet]
        public Task<IActionResult> AssigneeExternalCreate() =>
            SafeExecuteAsync(() => Task.FromResult<IActionResult>(PartialView("_AssigneeExternalCreate", new AssigneeExternalVm())), _log);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AssigneeExternalCreate(AssigneeExternalVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (!ModelState.IsValid) return BadRequest(new { ok = false, message = "Eksik/hatalı alan var." });

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

        
        [HttpGet]
        public Task<IActionResult> AssigneeFromUser() =>
            SafeExecuteAsync(async () =>
            {
                var users = await _db.Users.AsNoTracking().OrderBy(x => x.FullName).Take(500).ToListAsync();
                return PartialView("_AssigneeFromUser", new AssigneeFromUserVm { Users = users });
            }, _log);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AssigneeFromUser(AssigneeFromUserVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(vm.UserId))
                    return BadRequest(new { ok = false, message = "Kullanıcı seç." });

                var user = await _userManager.FindByIdAsync(vm.UserId);
                if (user is null) return BadRequest(new { ok = false, message = "Kullanıcı bulunamadı." });

                var existing = await _db.Assignees.FirstOrDefaultAsync(x => x.UserId == user.Id, ct);
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

        
        [HttpGet]
        public Task<IActionResult> AssignmentCreate(CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var vm = new AssignmentCreateVm
                {
                    AvailableAssets = await _db.Assets.AsNoTracking().Where(x => x.Status == AssetStatus.Available).OrderBy(x => x.Brand).ToListAsync(ct),
                    RecentAssignees = await _db.Assignees.AsNoTracking().OrderByDescending(x => x.Id).Take(50).ToListAsync(ct)
                };
                return PartialView("_AssignmentCreate", vm);
            }, _log);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AssignmentCreate(AssignmentCreatePostVm vm, CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                if (vm.AssigneeId <= 0 || vm.AssetIds.Count == 0)
                    return BadRequest(new { ok = false, message = "Kişi ve en az 1 cihaz seç." });

                var assets = await _db.Assets.Where(x => vm.AssetIds.Contains(x.Id)).ToListAsync(ct);
                if (assets.Any(x => x.Status != AssetStatus.Available))
                    return BadRequest(new { ok = false, message = "Seçilen cihazlardan biri müsait değil." });

                var assignment = new Assignment
                {
                    AssigneeId = vm.AssigneeId,
                    Notes = vm.Notes
                };

                foreach (var a in assets)
                {
                    a.Status = AssetStatus.InUse;
                    assignment.Items.Add(new AssignmentItem { AssetId = a.Id, ConditionOut = vm.ConditionOut });
                }

                _db.Assignments.Add(assignment);
                await _db.SaveChangesAsync(ct);

                return Ok(new { ok = true, assignmentId = assignment.Id });
            }, _log);

    }
}
