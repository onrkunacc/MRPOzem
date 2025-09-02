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

        // Gönderim Kaydı
        public async Task AddShipmentAsync(int hospitalId, int testId, DateTime month, int sentCount)
        {
            var shipment = await _context.MonthlyShipments
                .FirstOrDefaultAsync(s => s.HospitalID == hospitalId && s.TestID == testId && s.Month == month);

            if (shipment == null)
            {
                shipment = new MonthlyShipment
                {
                    HospitalID = hospitalId,
                    TestID = testId,
                    Month = month,
                    SentCount = sentCount
                };
                _context.MonthlyShipments.Add(shipment);
            }
            else
            {
                shipment.SentCount += sentCount; // aynı ayda tekrar eklenirse üzerine ekle
                _context.MonthlyShipments.Update(shipment);
            }

            await _context.SaveChangesAsync();
        }

        // Gönderim Listesi
        public async Task<List<MonthlyShipment>> GetShipmentsAsync(int hospitalId, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.MonthlyShipments
                .Include(s => s.Hospital)
                .Include(s => s.Test)
                .AsQueryable();

            if (hospitalId > 0)
                query = query.Where(s => s.HospitalID == hospitalId);

            if (startDate.HasValue)
                query = query.Where(s => s.Month >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.Month <= endDate.Value);

            return await query.OrderByDescending(s => s.Month).ToListAsync();
        }

        // Kullanım Kaydı
        public async Task AddUsageAsync(int hospitalId, int testId, DateTime month, int usedCount)
        {
            var usage = await _context.MonthlyStatistics
                .FirstOrDefaultAsync(u => u.HospitalID == hospitalId && u.TestID == testId && u.Month == month);

            if (usage == null)
            {
                usage = new MonthlyStatistics
                {
                    HospitalID = hospitalId,
                    TestID = testId,
                    Month = month,
                    TestCount = usedCount
                };
                _context.MonthlyStatistics.Add(usage);
            }
            else
            {
                usage.TestCount += usedCount; // aynı ay tekrar giriş yapılırsa ekle
                _context.MonthlyStatistics.Update(usage);
            }

            await _context.SaveChangesAsync();
        }

        // Kullanım Listesi
        public async Task<List<MonthlyStatistics>> GetUsagesAsync(int hospitalId, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.MonthlyStatistics
                .Include(u => u.Hospital)
                .Include(u => u.Test)
                .AsQueryable();

            if (hospitalId > 0)
                query = query.Where(u => u.HospitalID == hospitalId);

            if (startDate.HasValue)
                query = query.Where(u => u.Month >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(u => u.Month <= endDate.Value);

            return await query.OrderByDescending(u => u.Month).ToListAsync();
        }

        // Raporlama (Kalan Hesabı)
        public async Task<List<HospitalTestBalanceDTO>> GetHospitalTestBalanceAsync(int hospitalId, DateTime? startDate, DateTime? endDate)
        {
            var shipments = await GetShipmentsAsync(hospitalId, startDate, endDate);
            var usages = await GetUsagesAsync(hospitalId, startDate, endDate);

            var balance = shipments
                .GroupBy(s => new { s.HospitalID, s.Hospital.HospitalName, s.TestID, s.Test.TestName })
                .Select(g => new HospitalTestBalanceDTO
                {
                    HospitalID = g.Key.HospitalID,
                    HospitalName = g.Key.HospitalName,
                    TestID = g.Key.TestID,
                    TestName = g.Key.TestName,
                    TotalSent = g.Sum(x => x.SentCount),
                    TotalUsed = usages
                        .Where(u => u.HospitalID == g.Key.HospitalID && u.TestID == g.Key.TestID)
                        .Sum(u => u.TestCount)
                })
                .ToList();

            return balance;
        }
    }
}
