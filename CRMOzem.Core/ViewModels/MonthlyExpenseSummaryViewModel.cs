using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class MonthlyExpenseSummaryViewModel
    {
        public int Month {  get; set; }
        public decimal TotalAmount { get; set; }
        public string MonthName
        {
            get
            {
                return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);
            }
        }

    }
}
