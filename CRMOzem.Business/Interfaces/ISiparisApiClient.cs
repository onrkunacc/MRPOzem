using CRMOzem.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ISiparisApiClient
    {
        /// <summary>PHP /api/orders.php'den siparişleri çek. sinceId > 0 ise sadece daha yeni id'leri alır.</summary>
        Task<SiparisApiResponseDto?> GetOrdersAsync(
            int status = 1,
            int sinceId = 0,
            int limit = 200,
            CancellationToken ct = default);
    }
}
