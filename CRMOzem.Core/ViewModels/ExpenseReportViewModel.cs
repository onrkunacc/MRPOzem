using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class ExpenseReportViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public DateTime MonthYear { get; set; }
        public decimal TotalAmount { get; set; }
        public List<ExpenseDetailViewModel> ExpenseDetails { get; set; } = new();
    }
}
