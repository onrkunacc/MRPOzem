using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Tender
    {
        public int Id { get; set; }
        public string Name { get; set; } // İhale adı/birimi
        public string Type { get; set; } // İhale şekli
        public string RegistrationNumber { get; set; } // Kayıt No

        public DateTime TenderDate { get; set; }
        public int DurationMonths { get; set; } // İhale süresi (ay)

        [ValidateNever] public ICollection<TenderTest> Tests { get; set; } = new List<TenderTest>();
        [ValidateNever] public ICollection<TenderExtraCost> ExtraCosts { get; set; } = new List<TenderExtraCost>();
        [ValidateNever] public ICollection<TenderPersonnel> Personnels { get; set; } = new List<TenderPersonnel>();

        public decimal TotalTestCost { get; set; }
        public decimal TotalExtraCost { get; set; }
        public decimal TotalPersonnelCost { get; set; }
        public decimal OverallCost { get; set; }
    }
}
