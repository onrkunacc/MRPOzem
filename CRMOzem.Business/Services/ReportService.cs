using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.Core.ViewModels.Admin;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using X.PagedList.Extensions;

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
        //son eklenen methodlar 
        public async Task<AdminExpenseIndexVm> GetAdminIndexAsync(AdminExpenseFilterVm filter, int pageSize = 20, CancellationToken ct = default)
        {
            // 1) Tüm raporları + detayları çek (filtre bazlı)
            var query = _context.ExpenseReports
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.ExpenseDetails)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.UserId))
                query = query.Where(r => r.UserId == filter.UserId);

            if (filter.Year.HasValue)
                query = query.Where(r => r.MonthYear.Year == filter.Year.Value);

            if (filter.Month.HasValue)
                query = query.Where(r => r.MonthYear.Month == filter.Month.Value);

            var reports = await query.ToListAsync(ct);

            // 2) Kategori / Min / Max filtreleri detaya uygulanır
            if (!string.IsNullOrWhiteSpace(filter.Category))
            {
                reports = reports
                    .Where(r => r.ExpenseDetails.Any(d => d.Category == filter.Category))
                    .ToList();
            }

            if (filter.MinAmount.HasValue)
            {
                reports = reports.Where(r => r.ExpenseDetails.Sum(d => d.Amount) >= filter.MinAmount.Value).ToList();
            }

            if (filter.MaxAmount.HasValue)
            {
                reports = reports.Where(r => r.ExpenseDetails.Sum(d => d.Amount) <= filter.MaxAmount.Value).ToList();
            }

            // 3) Satır VM'ine çevir
            var rows = reports.Select(r => new ExpenseReportRowVm
            {
                Id = r.Id,
                UserId = r.UserId,
                UserName = r.User?.FullName ?? "Bilinmeyen",
                MonthYear = r.MonthYear,
                TotalAmount = r.ExpenseDetails?.Sum(d => d.Amount) ?? r.TotalAmount,
                IslemSayisi = r.ExpenseDetails?.Count ?? 0,
                Sparkline = new() // aşağıda doldurulacak
            }).ToList();

            // 4) Her kullanıcı için son 6 ayın sparkline'ı (toplu sorgu)
            var userIds = rows.Select(r => r.UserId).Distinct().ToList();
            var sixMonthsAgo = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);

            var sparkData = await _context.ExpenseReports.AsNoTracking()
                .Where(r => userIds.Contains(r.UserId) && r.MonthYear >= sixMonthsAgo)
                .Select(r => new
                {
                    r.UserId,
                    r.MonthYear,
                    Total = r.ExpenseDetails.Sum(d => d.Amount)
                })
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                var spark = new List<decimal>();
                for (int i = 0; i < 6; i++)
                {
                    var month = sixMonthsAgo.AddMonths(i);
                    var val = sparkData
                        .Where(x => x.UserId == row.UserId && x.MonthYear.Year == month.Year && x.MonthYear.Month == month.Month)
                        .Sum(x => x.Total);
                    spark.Add(val);
                }
                row.Sparkline = spark;
            }

            // 5) Sıralama
            rows = (filter.SortColumn, filter.SortDirection) switch
            {
                ("TotalAmount", "asc") => rows.OrderBy(r => r.TotalAmount).ToList(),
                ("TotalAmount", _) => rows.OrderByDescending(r => r.TotalAmount).ToList(),
                ("UserName", "asc") => rows.OrderBy(r => r.UserName).ToList(),
                ("UserName", "desc") => rows.OrderByDescending(r => r.UserName).ToList(),
                ("MonthYear", "asc") => rows.OrderBy(r => r.MonthYear).ToList(),
                _ => rows.OrderByDescending(r => r.MonthYear).ToList()
            };

            // 6) Summary
            var summary = new ExpenseSummaryVm
            {
                SeciliDonemToplam = rows.Sum(r => r.TotalAmount),
                SeciliDonemIslemSayisi = rows.Sum(r => r.IslemSayisi),
                OrtalamaIslemTutari = rows.Sum(r => r.IslemSayisi) > 0
                    ? Math.Round(rows.Sum(r => r.TotalAmount) / rows.Sum(r => r.IslemSayisi), 2)
                    : 0
            };

            var yilToplam = await _context.ExpenseReports.AsNoTracking()
                .Where(r => r.MonthYear.Year == (filter.Year ?? DateTime.Now.Year))
                .Where(r => string.IsNullOrEmpty(filter.UserId) || r.UserId == filter.UserId)
                .SelectMany(r => r.ExpenseDetails)
                .SumAsync(d => (decimal?)d.Amount, ct) ?? 0;
            summary.YilToplam = yilToplam;

            var enYuksekUser = rows
                .GroupBy(r => new { r.UserId, r.UserName })
                .Select(g => new TopUserMiniVm
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.UserName,
                    Toplam = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.Toplam)
                .FirstOrDefault();
            summary.EnYuksekHarcayan = enYuksekUser;

            var enYuksekTek = reports
                .SelectMany(r => r.ExpenseDetails.Select(d => new { r, d }))
                .OrderByDescending(x => x.d.Amount)
                .FirstOrDefault();

            if (enYuksekTek != null)
            {
                summary.EnYuksekTekHarcama = new ExpenseHighlightVm
                {
                    ReportId = enYuksekTek.r.Id,
                    Aciklama = enYuksekTek.d.Description,
                    Kategori = enYuksekTek.d.Category,
                    FullName = enYuksekTek.r.User?.FullName ?? "Bilinmeyen",
                    Tarih = enYuksekTek.d.Date,
                    Tutar = enYuksekTek.d.Amount
                };
            }

            // 7) Dropdown verisi
            var users = await _context.Users.AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem { Value = u.Id, Text = u.FullName })
                .ToListAsync(ct);

            var years = await _context.ExpenseReports.AsNoTracking()
                .Select(r => r.MonthYear.Year).Distinct().OrderByDescending(y => y).ToListAsync(ct);

            var categories = await _context.ExpenseDetails.AsNoTracking()
                .Select(d => d.Category).Distinct().OrderBy(c => c).ToListAsync(ct);

            // 8) Sayfalama
            var paged = rows.ToPagedList(filter.Page, pageSize);

            return new AdminExpenseIndexVm
            {
                Filter = filter,
                Summary = summary,
                PagedReports = paged,
                Users = users,
                AvailableYears = years,
                AvailableCategories = categories
            };
        }

        public async Task<AdminExpenseDetailsVm?> GetAdminDetailsAsync(int reportId, CancellationToken ct = default)
        {
            var report = await _context.ExpenseReports.AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.ExpenseDetails)
                .FirstOrDefaultAsync(r => r.Id == reportId, ct);

            if (report == null) return null;

            var details = report.ExpenseDetails ?? new List<ExpenseDetail>();
            var toplam = details.Sum(d => d.Amount);
            var top3Ids = details.OrderByDescending(d => d.Amount).Take(3).Select(d => d.Id).ToList();

            var vm = new AdminExpenseDetailsVm
            {
                ReportId = report.Id,
                UserId = report.UserId,
                UserName = report.User?.FullName ?? "Bilinmeyen",
                MonthYear = report.MonthYear,
                Toplam = toplam,
                IslemSayisi = details.Count,
                OrtalamaIslem = details.Count > 0 ? Math.Round(toplam / details.Count, 2) : 0
            };

            var enYuksek = details.OrderByDescending(d => d.Amount).FirstOrDefault();
            if (enYuksek != null)
            {
                vm.EnYuksek = new ExpenseDetailHighlightVm
                {
                    ExpenseId = enYuksek.Id,
                    Aciklama = enYuksek.Description,
                    Kategori = enYuksek.Category,
                    Tarih = enYuksek.Date,
                    Tutar = enYuksek.Amount
                };
            }

            vm.KategoriDagilim = details
                .GroupBy(d => d.Category)
                .Select(g => new CategoryBreakdownVm
                {
                    Kategori = g.Key,
                    Toplam = g.Sum(x => x.Amount),
                    IslemSayisi = g.Count(),
                    Yuzde = toplam > 0 ? Math.Round((g.Sum(x => x.Amount) / toplam) * 100, 1) : 0
                })
                .OrderByDescending(c => c.Toplam)
                .ToList();

            vm.Detaylar = details
                .OrderByDescending(d => d.Date)
                .Select(d => new ExpenseDetailRowVm
                {
                    Id = d.Id,
                    Tarih = d.Date,
                    Kategori = d.Category,
                    Aciklama = d.Description,
                    OdemeYontemi = d.PaymentMethod.ToString(),
                    Tutar = d.Amount,
                    YuksekRank = top3Ids.IndexOf(d.Id) is int idx && idx >= 0 ? idx + 1 : null
                })
                .ToList();

            return vm;
        }

        public async Task<AdminExpenseChartsVm> GetAdminChartsAsync(string? userId, int? year, CancellationToken ct = default)
        {
            int targetYear = year ?? DateTime.Now.Year;

            // Tüm detaylar (filtreli)
            var detailQuery = _context.ExpenseDetails.AsNoTracking()
                .Include(d => d.Report).ThenInclude(r => r.User)
                .Where(d => d.Date.Year == targetYear);

            if (!string.IsNullOrEmpty(userId))
                detailQuery = detailQuery.Where(d => d.Report.UserId == userId);

            var details = await detailQuery.ToListAsync(ct);

            // Aylık trend (12 ay)
            var aylik = new List<MonthlyTrendPointVm>();
            var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            for (int m = 1; m <= 12; m++)
            {
                var ayToplam = details.Where(d => d.Date.Month == m).Sum(d => d.Amount);
                aylik.Add(new MonthlyTrendPointVm
                {
                    Year = targetYear,
                    Month = m,
                    Label = new DateTime(targetYear, m, 1).ToString("MMM", tr),
                    Toplam = ayToplam
                });
            }

            // Kullanıcı karşılaştırması (sadece kullanıcı filtresi yokken anlamlı)
            var kullaniciKarsi = details
                .GroupBy(d => new { d.Report.UserId, d.Report.User!.FullName })
                .Select(g => new UserComparisonVm
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName ?? "Bilinmeyen",
                    Toplam = g.Sum(x => x.Amount),
                    IslemSayisi = g.Count()
                })
                .OrderByDescending(x => x.Toplam)
                .Take(10)
                .ToList();

            // Kategori dağılımı
            var toplamAmount = details.Sum(d => d.Amount);
            var kategori = details
                .GroupBy(d => d.Category)
                .Select(g => new CategoryBreakdownVm
                {
                    Kategori = g.Key,
                    Toplam = g.Sum(x => x.Amount),
                    IslemSayisi = g.Count(),
                    Yuzde = toplamAmount > 0 ? Math.Round((g.Sum(x => x.Amount) / toplamAmount) * 100, 1) : 0
                })
                .OrderByDescending(x => x.Toplam)
                .ToList();

            // En yüksek 10 harcama
            var top10 = details
                .OrderByDescending(d => d.Amount)
                .Take(10)
                .Select(d => new TopExpenseVm
                {
                    ReportId = d.ReportId,
                    FullName = d.Report.User?.FullName ?? "Bilinmeyen",
                    Kategori = d.Category,
                    Aciklama = d.Description,
                    Tarih = d.Date,
                    Tutar = d.Amount
                })
                .ToList();

            // Dropdown'lar
            var users = await _context.Users.AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem { Value = u.Id, Text = u.FullName })
                .ToListAsync(ct);

            var years = await _context.ExpenseReports.AsNoTracking()
                .Select(r => r.MonthYear.Year).Distinct().OrderByDescending(y => y).ToListAsync(ct);

            return new AdminExpenseChartsVm
            {
                SelectedUserId = userId,
                SelectedYear = targetYear,
                Users = users,
                AvailableYears = years,
                AylikTrend = aylik,
                KullaniciKarsilastirma = kullaniciKarsi,
                KategoriDagilim = kategori,
                EnYuksek10 = top10,
                YilToplam = toplamAmount,
                AylikOrtalama = aylik.Any(x => x.Toplam > 0)
                    ? Math.Round(toplamAmount / aylik.Count(x => x.Toplam > 0), 2)
                    : 0
            };
        }
    }
}
