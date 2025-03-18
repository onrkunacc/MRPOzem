using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.ViewModels;

namespace CRMOzem.Business.Interfaces
{
    public interface IReportService
    {
        Task<List<ExpenseReportViewModel>> GetAllReportsAsync();
        Task<List<ExpenseReportViewModel>> GetReportsByUserIdAsync(string userId);
        Task<List<ExpenseReportViewModel>> GetReportsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<MonthlyExpenseSummaryViewModel>> GetMonthlyExpenseSummaryAsync(int year);
    }
}
