using CRMOzem.Core.Inventory;
using CRMOzem.DataAccess.Context;
using CRMOzem.Web.Controllers;
using CRMOzem.Web.Vm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace CRMOzem.Web.Areas.IT.Controllers
{
    [Area("IT")]
    [Authorize(Roles = "IT")]
    public sealed class InventoryController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<InventoryController> _log;

        public InventoryController(ApplicationDbContext db, ILogger<InventoryController> log)
        {
            _db = db;
            _log = log;
        }

        public Task<IActionResult> Index(CancellationToken ct) =>
        SafeExecuteAsync(async () =>
        {
            var brandSummaryRaw = await _db.Assets.AsNoTracking()
                .GroupBy(a => new { a.Category, a.Brand, a.Model, a.Status })
                .Select(g => new
                {
                    g.Key.Category,
                    g.Key.Brand,
                    g.Key.Model,
                    Status = g.Key.Status,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(100)
                .ToListAsync(ct);

            var vm = new InventoryDashboardVm
            {
                Assets = await _db.Assets.AsNoTracking().OrderByDescending(x => x.Id).Take(200).ToListAsync(ct),
                ActiveItems = await _db.AssignmentItems.AsNoTracking()
                    .Where(x => x.ReturnedAtUtc == null)
                    .Include(x => x.Assignment)!.ThenInclude(a => a.Assignee)
                    .Include(x => x.Asset)
                    .OrderBy(x => x.Assignment!.Assignee!.FullName)
                    .ToListAsync(ct),
                BrandSummary = brandSummaryRaw
                    .Select(x => new BrandSummaryRow(x.Category, x.Brand, x.Model, x.Status.ToString(), x.Count))
                    .ToList()
            };

            return View(vm);
        }, _log);

        // Partials
        [HttpGet]
        public Task<IActionResult> AssetsTable(CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var list = await _db.Assets.AsNoTracking()
                    .OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);

                return PartialView("Partials/_AssetsTable", list);
            }, _log);

        [HttpGet]
        public Task<IActionResult> ActiveAssignmentsTable(CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var rows = await _db.AssignmentItems.AsNoTracking()
                    .Where(x => x.ReturnedAtUtc == null)
                    .Include(x => x.Assignment)!.ThenInclude(a => a.Assignee)
                    .Include(x => x.Asset)
                    .OrderBy(x => x.Assignment!.Assignee!.FullName)
                    .ToListAsync(ct);

                return PartialView("Partials/_ActiveAssignmentsTable", rows);
            }, _log);

        [HttpGet]
        public Task<IActionResult> BrandSummaryTable(CancellationToken ct) =>
            SafeExecuteAsync(async () =>
            {
                var raw = await _db.Assets.AsNoTracking()
                    .GroupBy(a => new { a.Category, a.Brand, a.Model, a.Status })
                    .Select(g => new { g.Key.Category, g.Key.Brand, g.Key.Model, Status = g.Key.Status, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(100)
                    .ToListAsync(ct);

                var rows = raw
                    .Select(x => new BrandSummaryRow(x.Category, x.Brand, x.Model, x.Status.ToString(), x.Count))
                    .ToList();

                return PartialView("Partials/_BrandSummaryTable", rows);
            }, _log);


        public sealed record BrandSummaryRow(string Category, string Brand, string Model, string Status, int Count);
    }
}
