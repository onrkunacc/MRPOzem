using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TenderExtraCost
    {
        public int Id { get; set; }
        public int TenderId { get; set; }

        public string Name { get; set; }
        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
        public string Currency { get; set; }   // USD, EUR, TRY
        public decimal ExchangeRate { get; set; }

        public decimal Total => Quantity * UnitPrice * ExchangeRate;

        [ValidateNever] public Tender Tender { get; set; } // nav doğrulamasını kapat
    }
}
