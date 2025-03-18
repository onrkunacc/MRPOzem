using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;

namespace CRMOzem.Business.Interfaces
{
    public interface IExpenseReportService
    {
        Task<List<ExpenseReportViewModel>> GetUserReportsAsync(string userId);
        Task<ExpenseReport> CreateReportAsync(string userId, DateTime monthYear);
        Task<bool> AddExpenseAsync(ExpenseDetailViewModel model,string userId);
        Task<byte[]> ExportToExcelAsync(int reportId);
        Task<ExpenseReportViewModel> GetReportByIdAsync(int reportId);

        Task<ExpenseDetail> GetExpenseByIdAsync(int id);
        Task<bool> UpdateExpenseAsync(ExpenseDetail expense);
        Task<List<MonthlyExpenseSummaryViewModel>> GetMonthlyExpensesAsync(string userId = null);
        Task<decimal> GetCurrentMonthExpenseAsync(string userId);
    }
}
