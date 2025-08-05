using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class BillingCycleReportViewModel
    {
        public int Year { get; set; }  // Ekstre Yılı 
        public int Month { get; set; } // Ekstre Ayı 
        public string MonthName { get; set; } // Örn: "Ocak", "Şubat"
        public decimal TotalAmount { get; set; } // Ayın toplam ekstresi
        public List<ExpenseReportViewModel> ExpenseReports { get; set; } = new();
        public List<ExpenseDetailViewModel> ExpenseDetails { get; set; } = new(); 
    }
}
