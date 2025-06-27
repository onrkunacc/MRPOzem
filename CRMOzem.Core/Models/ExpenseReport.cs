using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Core.Models
{
    public class ExpenseReport
    {
        public int Id { get; set; } 
        public string UserId { get; set; }
        public DateTime MonthYear { get; set; }
        [Precision(18,2)]
        public decimal TotalAmount { get; set; } = 0;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public virtual ApplicationUser User { get; set; }
        public virtual List<ExpenseDetail> ExpenseDetails { get; set; }

    }
}
