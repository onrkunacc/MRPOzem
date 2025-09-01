using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class AddExpenseInputModel
    {
        [Required] public int ReportId { get; set; }
        [Required] public DateTime Date { get; set; }
        [Required, StringLength(128)] public string Category { get; set; }
        [Required, StringLength(512)] public string Description { get; set; }
        [Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
        [Required] public PaymentMethod PaymentMethod { get; set; }
        public bool UserConfirmed { get; set; } = false;
    }

}
