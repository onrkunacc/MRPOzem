using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly ApplicationDbContext _context;

        public StatisticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        private static DateTime NormalizeMonth(DateTime month)
            => new DateTime(month.Year, month.Month, 1);

        private static (DateTime? start, DateTime? end) NormalizeRange(DateTime? startDate, DateTime? endDate)
        {
            DateTime? start = startDate.HasValue ? new DateTime(startDate.Value.Year, startDate.Value.Month, 1) : null;
            DateTime? end = endDate.HasValue ? new DateTime(endDate.Value.Year, endDate.Value.Month, DateTime.DaysInMonth(endDate.Value.Year, endDate.Value.Month)) : null;
            return (start, end);
        }

        public async Task AddShipmentAsync(int hospitalId, int testId, DateTime month, int sentCount, CancellationToken ct = default)
        {
            if (sentCount < 0) throw new ArgumentOutOfRangeException(nameof(sentCount));
            var normalized = NormalizeMonth(month);

            var shipment = await _context.MonthlyShipments
                .FirstOrDefaultAsync(s => s.HospitalID == hospitalId && s.TestID == testId && s.Month == normalized, ct);

            if (shipment == null)
            {
                shipment = new MonthlyShipment
                {
                    HospitalID = hospitalId,
                    TestID = testId,
                    Month = normalized,
                    SentCount = sentCount
                };
                _context.MonthlyShipments.Add(shipment);
            }
            else
            {
                shipment.SentCount += sentCount; // tekrar girişlerde birikir (istenen davranış)
                _context.MonthlyShipments.Update(shipment);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<MonthlyShipment>> GetShipmentsAsync(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
        {
            var (start, end) = NormalizeRange(startDate, endDate);

            var query = _context.MonthlyShipments
                .AsNoTracking()
                .Include(s => s.Hospital)
                .Include(s => s.Test)
                .AsQueryable();

            if (hospitalId > 0)
                query = query.Where(s => s.HospitalID == hospitalId);
            if (start.HasValue)
                query = query.Where(s => s.Month >= start.Value);
            if (end.HasValue)
                query = query.Where(s => s.Month <= end.Value);

            return await query.OrderByDescending(s => s.Month).ThenBy(s => s.TestID).ToListAsync(ct);
        }

        public async Task AddUsageAsync(int hospitalId, int testId, DateTime month, int usedCount, CancellationToken ct = default)
        {
            if (usedCount < 0) throw new ArgumentOutOfRangeException(nameof(usedCount));
            var normalized = NormalizeMonth(month);

            var usage = await _context.MonthlyStatistics
                .FirstOrDefaultAsync(u => u.HospitalID == hospitalId && u.TestID == testId && u.Month == normalized, ct);

            if (usage == null)
            {
                usage = new MonthlyStatistics
                {
                    HospitalID = hospitalId,
                    TestID = testId,
                    Month = normalized,
                    TestCount = usedCount
                };
                _context.MonthlyStatistics.Add(usage);
            }
            else
            {
                usage.TestCount += usedCount;
                _context.MonthlyStatistics.Update(usage);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<MonthlyStatistics>> GetUsagesAsync(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
        {
            var (start, end) = NormalizeRange(startDate, endDate);

            var query = _context.MonthlyStatistics
                .AsNoTracking()
                .Include(u => u.Hospital)
                .Include(u => u.Test)
                .AsQueryable();

            if (hospitalId > 0)
                query = query.Where(u => u.HospitalID == hospitalId);
            if (start.HasValue)
                query = query.Where(u => u.Month >= start.Value);
            if (end.HasValue)
                query = query.Where(u => u.Month <= end.Value);

            return await query.OrderByDescending(u => u.Month).ThenBy(u => u.TestID).ToListAsync(ct);
        }

        public async Task<List<HospitalTestBalanceDTO>> GetHospitalTestBalanceAsync(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
        {
            var shipments = await GetShipmentsAsync(hospitalId, startDate, endDate, ct);
            var usages = await GetUsagesAsync(hospitalId, startDate, endDate, ct);

            // Aylık bazda topla
            var sentByMonth = shipments
                .GroupBy(s => new { s.HospitalID, s.Hospital.HospitalName, s.TestID, s.Test.TestName, Month = NormalizeMonth(s.Month) })
                .Select(g => new
                {
                    g.Key.HospitalID,
                    g.Key.HospitalName,
                    g.Key.TestID,
                    g.Key.TestName,
                    g.Key.Month,
                    TotalSent = g.Sum(x => x.SentCount)
                });

            var usedByMonth = usages
                .GroupBy(u => new { u.HospitalID, u.TestID, Month = NormalizeMonth(u.Month) })
                .Select(g => new
                {
                    g.Key.HospitalID,
                    g.Key.TestID,
                    g.Key.Month,
                    TotalUsed = g.Sum(x => x.TestCount)
                });

            // Left join: gönderim olan aylar baz; kullanım olmayan aylar 0
            var result = (from s in sentByMonth
                          join u in usedByMonth
                            on new { s.HospitalID, s.TestID, s.Month }
                            equals new { u.HospitalID, u.TestID, u.Month }
                            into gj
                          from u in gj.DefaultIfEmpty()
                          select new HospitalTestBalanceDTO
                          {
                              HospitalID = s.HospitalID,
                              HospitalName = s.HospitalName,
                              TestID = s.TestID,
                              TestName = s.TestName,
                              Month = s.Month,
                              TotalSent = s.TotalSent,
                              TotalUsed = u?.TotalUsed ?? 0
                          })
                          .OrderBy(x => x.HospitalName)
                          .ThenBy(x => x.TestName)
                          .ThenBy(x => x.Month)
                          .ToList();

            return result;
        }
    }
}
