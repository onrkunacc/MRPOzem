using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Core.Models
{
    public class ExpenseDetail
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        [Precision(18, 2)]
        public decimal Amount {  get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public virtual ExpenseReport Report {  get; set; }
        public int? OverrideCycleMonth { get; set; }  
        public int? OverrideCycleYear { get; set; }   

    }
}
