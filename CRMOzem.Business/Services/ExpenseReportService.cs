using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Business.Services
{
    public class ExpenseReportService : IExpenseReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExpenseReportService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<ExpenseReportViewModel>> GetUserReportsAsync(string userId)
        {
            var reports = await _context.ExpenseReports
                .Where(x => x.UserId == userId)
                .Include(x => x.ExpenseDetails)
                .ToListAsync();

            return reports.Select(r => new ExpenseReportViewModel
            {
                Id = r.Id,
                UserName = r.User?.UserName,
                MonthYear = r.MonthYear,
                TotalAmount = r.TotalAmount,
                ExpenseDetails = r.ExpenseDetails.Select(d => new ExpenseDetailViewModel
                {
                    Id = d.Id,
                    ReportId = d.ReportId,
                    Category = d.Category,
                    Description = d.Description,
                    Amount = d.Amount,
                    Date = d.Date,
                }).ToList()
            }).ToList();
        }

        public async Task<ExpenseReport> CreateReportAsync(string userId, DateTime monthYear)
        {
            var report = new ExpenseReport { UserId = userId, MonthYear = monthYear };
            await _context.ExpenseReports.AddAsync(report);
            await _context.SaveChangesAsync();
            return report;
        }

        public async Task<bool> AddExpenseAsync(ExpenseDetailViewModel model,string userId)
        {
            var reportMonth = new DateTime(model.Date.Year, model.Date.Month, 1);

            // Kullanıcının o aya ait masraf raporu var mı?
            var report = await _context.ExpenseReports
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MonthYear == reportMonth);

            // Eğer yoksa yeni rapor oluştur
            if (report == null)
            {
                report = new ExpenseReport
                {
                    UserId = userId,
                    MonthYear = reportMonth,
                    TotalAmount = 0
                };
                await _context.ExpenseReports.AddAsync(report);
                await _context.SaveChangesAsync();
            }

            // Masraf ekleme
            var expense = new ExpenseDetail
            {
                ReportId = report.Id,
                Category = model.Category,
                Description = model.Description,
                Amount = model.Amount,
                Date = model.Date,
                PaymentMethod = model.PaymentMethod
            };

            await _context.ExpenseDetails.AddAsync(expense);
            report.TotalAmount += model.Amount;  // Toplam tutarı güncelle
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<byte[]> ExportToExcelAsync(int reportId)
        {
            var report = await _context.ExpenseReports
                .Include(r => r.ExpenseDetails)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null) return null;

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Masraf Raporu");

                // **Şirket Logosu Sol Üstte**
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tinydash-master", "light", "assets", "images", "logo.jpeg");
                if (System.IO.File.Exists(logoPath))
                {
                    var picture = worksheet.AddPicture(logoPath)
                        .MoveTo(worksheet.Cell("A1")) // Logoyu A1 hücresine yerleştir
                        .Scale(0.5); // Logo boyutunu küçült
                }

                // **Başlıklar C7'den Başlıyor**
                worksheet.Cell(7, 3).Value = "Tarih";
                worksheet.Cell(7, 4).Value = "Kategori";
                worksheet.Cell(7, 5).Value = "Açıklama";
                worksheet.Cell(7, 6).Value = "Ödeme Yöntemi";
                worksheet.Cell(7, 7).Value = "Tutar (₺)";

                // **Başlık Stil**
                var headerRange = worksheet.Range("C7:G7");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0066CC"); // Mavi arka plan
                headerRange.Style.Font.FontColor = XLColor.White; // Beyaz yazı
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // **Masraf Detaylarını Tarihe Göre Sırala**
                int row = 8; // Veri girişini C8'den başlat
                foreach (var detail in report.ExpenseDetails.OrderBy(d => d.Date))
                {
                    worksheet.Cell(row, 3).Value = detail.Date.ToString("dd.MM.yyyy");
                    worksheet.Cell(row, 4).Value = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(detail.Category);
                    worksheet.Cell(row, 5).Value = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(detail.Description);
                    worksheet.Cell(row, 6).Value = detail.PaymentMethod.ToString();
                    worksheet.Cell(row, 7).Value = detail.Amount;
                    row++;
                }

                // **Toplam Tutar Sağ Alt Köşe**
                worksheet.Cell(row, 6).Value = "Toplam Tutar:";
                worksheet.Cell(row, 7).FormulaA1 = $"=SUM(G8:G{row - 1})";
                worksheet.Cell(row, 7).Style.Font.Bold = true;
                worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;

                // **İmza Alanı Sol Alt Köşe**
                worksheet.Cell(row + 2, 3).Value = "İmza :";
                worksheet.Cell(row + 2, 4).Value = "......................";
                worksheet.Range($"C{row + 2}:D{row + 2}").Style.Font.Italic = true;

                // **Sütunları Otomatik Genişlet**
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<ExpenseReportViewModel> GetReportByIdAsync(int reportId)
        {
            var report = await _context.ExpenseReports
                .Include(r => r.ExpenseDetails)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null) return null;

            return new ExpenseReportViewModel
            {
                Id = report.Id,
                UserId = report.UserId,
                UserName = report.User?.FullName,
                MonthYear = report.MonthYear,
                TotalAmount = report.TotalAmount,
                ExpenseDetails = report.ExpenseDetails.Select(d => new ExpenseDetailViewModel
                {
                    Id = d.Id,
                    ReportId = d.ReportId,
                    Category = d.Category,
                    Description = d.Description,
                    Amount = d.Amount,
                    Date = d.Date,
                    PaymentMethod = d.PaymentMethod,
                    OverrideCycleMonth = d.OverrideCycleMonth,
                    OverrideCycleYear = d.OverrideCycleYear
                }).ToList()
            };
        }
        public async Task<bool> DeleteExpenseAsync(int expenseId)
        {
            var expense = await _context.ExpenseDetails.FindAsync(expenseId);
            if (expense == null) return false;

            _context.ExpenseDetails.Remove(expense);

            // **İlgili raporun toplam tutarını güncelle**
            var report = await _context.ExpenseReports.FindAsync(expense.ReportId);
            if (report != null)
            {
                report.TotalAmount -= expense.Amount;
                _context.ExpenseReports.Update(report);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<ExpenseDetail> GetExpenseByIdAsync(int id)
        {
            return await _context.ExpenseDetails.FindAsync(id);
        }

        public async Task<bool> UpdateExpenseAsync(ExpenseDetail model)
        {
            try
            {
                var expense = await _context.ExpenseDetails.FindAsync(model.Id);
                if (expense == null) return false;

                expense.Amount = model.Amount;
                _context.ExpenseDetails.Update(expense);

                
                var report = await _context.ExpenseReports
                    .Include(r => r.ExpenseDetails)
                    .FirstOrDefaultAsync(r => r.Id == expense.ReportId);

                if (report != null)
                {
                    report.TotalAmount = report.ExpenseDetails.Sum(d => d.Amount);
                    _context.ExpenseReports.Update(report);
                }

                await _context.SaveChangesAsync(); 
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Güncelleme Hatası: {ex.Message}");
                return false;
            }
        }
        public async Task<List<MonthlyExpenseSummaryViewModel>> GetMonthlyExpensesAsync(string userId = null)
        {
            var query = _context.ExpenseReports.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(r => r.UserId == userId);
            }

            var monthlyExpenses = await query
                .GroupBy(r => new { r.MonthYear.Year, r.MonthYear.Month })
                .Select(g => new MonthlyExpenseSummaryViewModel
                {
                    Month = g.Key.Month,
                    TotalAmount = g.Sum(r => r.TotalAmount)
                })
                .OrderBy(m => m.Month)
                .ToListAsync();

            return monthlyExpenses;
        }


        public async Task<decimal> GetCurrentMonthExpenseAsync(string userId)
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var expenses = await _context.ExpenseReports
                .Where(e => e.UserId == userId && e.MonthYear.Month == currentMonth && e.MonthYear.Year == currentYear)
                .SumAsync(e => e.TotalAmount);

            return expenses;
        }


        private DateTime GetFirstMondayOfWeek(int year, int month, int weekNumber)
        {
            DateTime firstDayOfMonth = new DateTime(year, month, 1);

            // İlk haftanın pazartesini bul
            DateTime firstMonday = firstDayOfMonth;
            while (firstMonday.DayOfWeek != DayOfWeek.Monday)
            {
                firstMonday = firstMonday.AddDays(1);
            }

            // Kullanıcının seçtiği haftaya göre pazartesi gününü hesapla
            int daysToAdd = (weekNumber - 1) * 7; // 1. hafta = 0 gün, 2. hafta = 7 gün, vb.
            DateTime billingCycleDay = firstMonday.AddDays(daysToAdd);

            return billingCycleDay;
        }

        public async Task<List<BillingCycleReportViewModel>> GetUserBillingCycleReportsAsync(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.BillingCutoffDay < 1 || user.BillingCutoffDay > 28)
                return new List<BillingCycleReportViewModel>();

            var expenses = await _context.ExpenseDetails
                .Include(e => e.Report)
                .Where(e => e.Report.UserId == userId)
                .ToListAsync();

            var grouped = expenses
                .GroupBy(e =>
                {
                    // Override varsa öncelikli
                    int year = e.OverrideCycleYear ?? 0;
                    int month = e.OverrideCycleMonth ?? 0;

                    if (year > 0 && month > 0)
                        return new { Year = year, Month = month };

                    var d = e.Date;
                    int cutoff = user.BillingCutoffDay;

                    if (d.Day <= cutoff)
                    {
                        return new { Year = d.Year, Month = d.Month };
                    }
                    else
                    {
                        var shifted = d.AddMonths(1);
                        return new { Year = shifted.Year, Month = shifted.Month };
                    }
                })
                .Select(g => new BillingCycleReportViewModel
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    TotalAmount = g.Sum(x => x.Amount),
                    ExpenseDetails = g.OrderBy(x => x.Date).Select(d => new ExpenseDetailViewModel
                    {
                        Id = d.Id,
                        Category = d.Category,
                        Description = d.Description,
                        Amount = d.Amount,
                        Date = d.Date,
                        PaymentMethod = d.PaymentMethod,
                        OverrideCycleMonth = d.OverrideCycleMonth,
                        OverrideCycleYear = d.OverrideCycleYear
                    }).ToList()
                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            return grouped;
        }

        public async Task<List<BillingCycleReportViewModel>> GetUsersBillingCycleReportsAsync(string userId, int cutOffDay)
        {
            var expenseDetails = await _context.ExpenseDetails
                .Include(ed => ed.Report)
                .Where(ed => ed.Report.UserId == userId)
                .ToListAsync();

            var grouped = expenseDetails
                .Select(ed =>
                {
                    var date = ed.Date;

                    // Raporun başlangıç tarihi cutOffDay'e göre belirleniyor
                    DateTime periodStart = date.Day >= cutOffDay
                        ? new DateTime(date.Year, date.Month, cutOffDay)
                        : new DateTime(date.Year, date.Month, cutOffDay).AddMonths(-1);

                    return new
                    {
                        PeriodStart = periodStart,
                        Detail = ed
                    };
                })
                .GroupBy(x => new { x.PeriodStart.Year, x.PeriodStart.Month })
                .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
                .ToList();

            var result = new List<BillingCycleReportViewModel>();

            foreach (var group in grouped)
            {
                var expenseDetailVMs = group.Select(x => new ExpenseDetailViewModel
                {
                    Date = x.Detail.Date,
                    Amount = x.Detail.Amount,
                    Category = x.Detail.Category,
                    Description = x.Detail.Description,
                    PaymentMethod = x.Detail.PaymentMethod
                }).ToList();

                result.Add(new BillingCycleReportViewModel
                {
                    Year = group.Key.Year,
                    Month = group.Key.Month,
                    MonthName = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMMM", new CultureInfo("tr-TR")),
                    TotalAmount = expenseDetailVMs.Sum(d => d.Amount),
                    ExpenseDetails = expenseDetailVMs
                });
            }

            return result;
        }


    }
}
