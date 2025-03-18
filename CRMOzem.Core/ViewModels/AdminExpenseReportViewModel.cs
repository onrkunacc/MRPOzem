using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering; 

namespace CRMOzem.Core.ViewModels
{
    public class AdminExpenseReportViewModel
    {
        public List<ExpenseReportViewModel> Reports { get; set; } = new List<ExpenseReportViewModel>();
        public List<SelectListItem> Users { get; set; } = new List<SelectListItem>(); 
        public string SelectedUserId { get; set; }
        public List<MonthlyExpenseSummaryViewModel> MonthlyExpenses { get; set; } = new List<MonthlyExpenseSummaryViewModel>();
    }
}
