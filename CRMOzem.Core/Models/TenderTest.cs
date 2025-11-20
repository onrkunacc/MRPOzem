using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TenderTest
    {
        public int Id { get; set; }
        public int TenderId { get; set; }

        public int OrderNo { get; set; }
        public string SutCode { get; set; }
        public string Name { get; set; }
        public decimal SutPoint { get; set; }
        public decimal Quantity { get; set; }

        public decimal Multiplier { get; set; } // SUT Çarpanı
        public decimal TotalPoint => SutPoint * Quantity;

        public decimal TotalSutPrice => (SutPoint * Multiplier) * Quantity;

        [ValidateNever] public Tender Tender { get; set; }
    }
}
