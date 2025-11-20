using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public sealed class TenderCalculatorService : ITenderCalculatorService
    {
        public void Calculate(Tender tender)
        {
            if (tender == null) throw new ArgumentNullException(nameof(tender));

            // Koleksiyon güvenliği
            tender.Tests ??= Array.Empty<TenderTest>().ToList();
            tender.ExtraCosts ??= Array.Empty<TenderExtraCost>().ToList();
            tender.Personnels ??= Array.Empty<TenderPersonnel>().ToList();

            // Neden: Modelleri değiştirmiyoruz; her kalemin kendi hesaplanan property’sini kullanıyoruz.
            var testTotal = tender.Tests.Sum(t => ClampNonNegative(t.TotalSutPrice));
            var extraTotal = tender.ExtraCosts.Sum(e => ClampNonNegative(e.Total));
            var months = Math.Max(0, tender.DurationMonths);
            var personnelTotal = tender.Personnels.Sum(p => ClampNonNegative(p.TotalForTender(months)));

            tender.TotalTestCost = Round2(testTotal);
            tender.TotalExtraCost = Round2(extraTotal);
            tender.TotalPersonnelCost = Round2(personnelTotal);
            tender.OverallCost = Round2(tender.TotalTestCost + tender.TotalExtraCost + tender.TotalPersonnelCost);
        }

        // Sadece “negatifse 0” normalize eder. decimal için IsNaN yoktur.
        private static decimal ClampNonNegative(decimal v) => v < 0 ? 0 : v;
        private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }
}
