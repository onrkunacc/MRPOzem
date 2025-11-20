using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class TenderViewModel
    {
        public Tender Tender { get; set; }

        public List<TenderTest> Tests { get; set; } = new();
        public List<TenderExtraCost> ExtraCosts { get; set; } = new();
        public List<TenderPersonnel> Personnels { get; set; } = new();

        public decimal TotalTestCost { get; set; }
        public decimal TotalExtraCost { get; set; }
        public decimal TotalPersonnelCost { get; set; }
        public decimal OverallCost { get; set; }
    }
}
