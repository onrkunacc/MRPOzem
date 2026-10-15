using CRMOzem.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IFizikiSayimApiClient
    {
        /// <summary>Tek bir hastanenin aktif sayım stoklarını çeker. hospitalId null → tüm hastaneler.</summary>
        Task<StokApiResponseDto?> GetStocksAsync(
            int? hospitalId = null,
            int? sayimNo = null,
            CancellationToken ct = default);

        /// <summary>Bağlantı testi — sağlıklıysa true.</summary>
        Task<bool> PingAsync(CancellationToken ct = default);
    }
}
