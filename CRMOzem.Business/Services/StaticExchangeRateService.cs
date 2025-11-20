using CRMOzem.Business.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public sealed class StaticExchangeRateService : IExchangeRateService
    {
        private readonly ConcurrentDictionary<string, decimal> _rates = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["TRY"] = 1m,
            ["USD"] = 34.00m,
            ["EUR"] = 36.00m,
            ["GBP"] = 41.00m
        };

        public bool TryGetRate(string currencyCode, out decimal rateToTRY)
            => _rates.TryGetValue(currencyCode?.Trim() ?? "TRY", out rateToTRY);
    }
}
