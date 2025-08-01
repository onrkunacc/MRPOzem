using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Enums;

namespace CRMOzem.Core.ViewModels
{
    public class ExpenseDetailViewModel
    {
        public int Id { get; set; }
        public int ReportId { get; set; }

        [Required(ErrorMessage = "Kategori zorunludur.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Açıklama zorunludur.")]
        public string Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Geçerli bir tutar giriniz.")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public int? OverrideCycleMonth { get; set; }
        public int? OverrideCycleYear { get; set; }

    }
}
