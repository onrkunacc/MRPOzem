using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IExchangeRateService
    {
        /// <returns>true if found</returns>
        bool TryGetRate(string currencyCode, out decimal rateToTRY);
    }
}
