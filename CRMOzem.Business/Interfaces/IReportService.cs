using CRMOzem.Core.ViewModels;
using CRMOzem.Core.ViewModels.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IReportService
    {
        Task<List<ExpenseReportViewModel>> GetAllReportsAsync();
        Task<List<ExpenseReportViewModel>> GetReportsByUserIdAsync(string userId);
        Task<List<ExpenseReportViewModel>> GetReportsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<MonthlyExpenseSummaryViewModel>> GetMonthlyExpenseSummaryAsync(int year);

        Task<AdminExpenseIndexVm> GetAdminIndexAsync(AdminExpenseFilterVm filter, int pageSize = 20, CancellationToken ct = default);
        Task<AdminExpenseDetailsVm?> GetAdminDetailsAsync(int reportId, CancellationToken ct = default);
        Task<AdminExpenseChartsVm> GetAdminChartsAsync(string? userId, int? year, CancellationToken ct = default);
    }
}
