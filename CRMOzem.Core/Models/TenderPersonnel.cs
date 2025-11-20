using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TenderPersonnel
    {
        public int Id { get; set; }
        public int TenderId { get; set; }

        public string Role { get; set; }                // Görev
        public int Count { get; set; }                  // Personel sayısı
        public string Education { get; set; }           // Öğrenim durumu

        public string FeeDescription { get; set; }      // Ücret açıklaması
        public decimal UnitFee { get; set; }            // Brüt ücret
        public decimal MonthlyTotal => UnitFee * Count;

        public decimal TotalForTender(int months) => MonthlyTotal * months;

        [ValidateNever] public Tender Tender { get; set; } // nav doğrulamasını kapat
    }
}
