using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Business.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ExpenseReportViewModel>> GetAllReportsAsync()
        {
            var reports = await _context.ExpenseReports
                .Include(r => r.ExpenseDetails)
                .Include(r => r.User)
                .ToListAsync();

            return reports.Select(report => new ExpenseReportViewModel
            {
                Id = report.Id,
                UserId = report.UserId,
                UserName = report.User?.FullName ?? "Bilinmiyor",
                MonthYear = report.MonthYear, 
                TotalAmount = report.ExpenseDetails?.Sum(d => d.Amount) ?? 0,
                ExpenseDetails = report.ExpenseDetails?.Select(d => new ExpenseDetailViewModel
                {
                    Id = d.Id,
                    ReportId = d.ReportId,
                    Category = d.Category,
                    Description = d.Description,
                    Amount = d.Amount,
                    Date = d.Date,
                    PaymentMethod = d.PaymentMethod
                }).ToList() ?? new List<ExpenseDetailViewModel>()
            }).ToList();
        }

        public async Task<List<ExpenseReportViewModel>> GetReportsByUserIdAsync(string userId)
        {
            var reports = await _context.ExpenseReports
                .Where(r => r.UserId == userId)
                .Include(r => r.ExpenseDetails)
                .Include(r => r.User)
                .ToListAsync();

            return reports.Select(report => new ExpenseReportViewModel
            {
                Id = report.Id,
                UserId = report.UserId,
                UserName = report.User?.FullName ?? "Bilinmiyor",
                MonthYear = report.MonthYear,
                TotalAmount = report.ExpenseDetails?.Sum(d => d.Amount) ?? 0,
                ExpenseDetails = report.ExpenseDetails?.Select(d => new ExpenseDetailViewModel
                {
                    Id = d.Id,
                    ReportId = d.ReportId,
                    Category = d.Category,
                    Description = d.Description,
                    Amount = d.Amount,
                    Date = d.Date,
                    PaymentMethod = d.PaymentMethod
                }).ToList() ?? new List<ExpenseDetailViewModel>()
            }).ToList();
        }

        public async Task<List<ExpenseReportViewModel>> GetReportsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var reports = await _context.ExpenseReports
                .Where(r => r.MonthYear >= startDate && r.MonthYear <= endDate)
                .Include(r => r.ExpenseDetails)
                .Include(r => r.User)
                .ToListAsync();

            return reports.Select(report => new ExpenseReportViewModel
            {
                Id = report.Id,
                UserId = report.UserId,
                UserName = report.User?.FullName ?? "Bilinmiyor",
                MonthYear = report.MonthYear,
                TotalAmount = report.ExpenseDetails?.Sum(d => d.Amount) ?? 0,
                ExpenseDetails = report.ExpenseDetails?.Select(d => new ExpenseDetailViewModel
                {
                    Id = d.Id,
                    ReportId = d.ReportId,
                    Category = d.Category,
                    Description = d.Description,
                    Amount = d.Amount,
                    Date = d.Date,
                    PaymentMethod = d.PaymentMethod
                }).ToList() ?? new List<ExpenseDetailViewModel>()
            }).ToList();
        }

        public async Task<List<MonthlyExpenseSummaryViewModel>> GetMonthlyExpenseSummaryAsync(int year)
        {
            // Artık rapor bazlı toplam yerine detaylar üzerinden hesaplıyoruz
            var reports = await _context.ExpenseReports
                .Include(r => r.ExpenseDetails)
                .Where(r => r.MonthYear.Year == year)
                .ToListAsync();

            return reports
                .GroupBy(r => r.MonthYear.Month)
                .Select(g => new MonthlyExpenseSummaryViewModel
                {
                    Month = g.Key,
                    TotalAmount = g.Sum(r => r.ExpenseDetails.Sum(d => d.Amount))
                })
                .OrderBy(x => x.Month)
                .ToList();
        }
    }
}
