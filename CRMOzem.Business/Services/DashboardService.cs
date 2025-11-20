using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Dashboard;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public sealed class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;
        public DashboardService(ApplicationDbContext db) => _db = db;

        public async Task<KpiDto> GetKpisAsync(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
        {
            var start = DateRangeHelper.NormalizeStart(startDate);
            var end = DateRangeHelper.NormalizeEnd(endDate);

            var shipmentsQ = _db.MonthlyShipments.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);
            var usageQ = _db.MonthlyStatistics.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);

            if (hospitalId.GetValueOrDefault() > 0)
            {
                shipmentsQ = shipmentsQ.Where(x => x.HospitalID == hospitalId);
                usageQ = usageQ.Where(x => x.HospitalID == hospitalId);
            }

            var totalShipment = await shipmentsQ.SumAsync(x => (long)x.SentCount, ct);
            var totalUsage = await usageQ.SumAsync(x => (long)x.TestCount, ct);
            var uniqueTests = await shipmentsQ.Select(x => x.TestID).Union(usageQ.Select(x => x.TestID)).Distinct().CountAsync(ct);
            var activeHospitals = await shipmentsQ.Select(x => x.HospitalID).Union(usageQ.Select(x => x.HospitalID)).Distinct().CountAsync(ct);

            return new KpiDto
            {
                TotalShipment = totalShipment,
                TotalUsage = totalUsage,
                UniqueTests = uniqueTests,
                ActiveHospitals = activeHospitals
            };
        }

        public async Task<List<TrendPointDto>> GetTrendsAsync(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
        {
            var start = DateRangeHelper.NormalizeStart(startDate);
            var end = DateRangeHelper.NormalizeEnd(endDate);

            var shipmentsQ = _db.MonthlyShipments.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);
            var usageQ = _db.MonthlyStatistics.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);

            if (hospitalId.GetValueOrDefault() > 0)
            {
                shipmentsQ = shipmentsQ.Where(x => x.HospitalID == hospitalId);
                usageQ = usageQ.Where(x => x.HospitalID == hospitalId);
            }

            var s = await shipmentsQ
                .GroupBy(x => new { x.Month.Year, x.Month.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (long)x.SentCount) })
                .ToListAsync(ct);

            var u = await usageQ
                .GroupBy(x => new { x.Month.Year, x.Month.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, V = g.Sum(x => (long)x.TestCount) })
                .ToListAsync(ct);

            var months = s.Select(x => (x.Year, x.Month)).Union(u.Select(x => (x.Year, x.Month)))
                          .Distinct().OrderBy(x => x.Year).ThenBy(x => x.Month).ToList();

            return months.Select(mm => new TrendPointDto
            {
                Month = new DateTime(mm.Year, mm.Month, 1).ToString("yyyy-MM"),
                Shipment = s.FirstOrDefault(x => x.Year == mm.Year && x.Month == mm.Month)?.V ?? 0,
                Usage = u.FirstOrDefault(x => x.Year == mm.Year && x.Month == mm.Month)?.V ?? 0
            }).ToList();
        }

        public async Task<(List<TopItemDto> shipments, List<TopItemDto> usage)> GetTopTestsAsync(int? hospitalId, DateTime? startDate, DateTime? endDate, int take = 10, CancellationToken ct = default)
        {
            var start = DateRangeHelper.NormalizeStart(startDate);
            var end = DateRangeHelper.NormalizeEnd(endDate);

            var shipmentsQ = _db.MonthlyShipments.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);
            var usageQ = _db.MonthlyStatistics.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);

            if (hospitalId.GetValueOrDefault() > 0)
            {
                shipmentsQ = shipmentsQ.Where(x => x.HospitalID == hospitalId);
                usageQ = usageQ.Where(x => x.HospitalID == hospitalId);
            }

            var shipTop = await shipmentsQ
                .GroupBy(x => x.Test.TestName)
                .Select(g => new TopItemDto { Name = g.Key!, Value = g.Sum(x => (long)x.SentCount) })
                .OrderByDescending(x => x.Value).Take(take).ToListAsync(ct);

            var useTop = await usageQ
                .GroupBy(x => x.Test.TestName)
                .Select(g => new TopItemDto { Name = g.Key!, Value = g.Sum(x => (long)x.TestCount) })
                .OrderByDescending(x => x.Value).Take(take).ToListAsync(ct);

            return (shipTop, useTop);
        }

        public async Task<List<ByHospitalDto>> GetByHospitalAsync(DateTime? startDate, DateTime? endDate, int take = 12, CancellationToken ct = default)
        {
            var start = DateRangeHelper.NormalizeStart(startDate);
            var end = DateRangeHelper.NormalizeEnd(endDate);

            var shipmentsQ = _db.MonthlyShipments.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);
            var usageQ = _db.MonthlyStatistics.AsNoTracking().Where(x => x.Month >= start && x.Month <= end);

            var s = await shipmentsQ
                .GroupBy(x => x.Hospital.HospitalName)
                .Select(g => new { Name = g.Key!, V = g.Sum(x => (long)x.SentCount) })
                .OrderByDescending(x => x.V).Take(take).ToListAsync(ct);

            var u = await usageQ
                .GroupBy(x => x.Hospital.HospitalName)
                .Select(g => new { Name = g.Key!, V = g.Sum(x => (long)x.TestCount) })
                .OrderByDescending(x => x.V).Take(take).ToListAsync(ct);

            var names = s.Select(x => x.Name).Union(u.Select(x => x.Name)).Distinct().ToList();

            return names.Select(n => new ByHospitalDto
            {
                Hospital = n,
                Shipment = s.FirstOrDefault(x => x.Name == n)?.V ?? 0,
                Usage = u.FirstOrDefault(x => x.Name == n)?.V ?? 0
            }).ToList();
        }
    }
}
