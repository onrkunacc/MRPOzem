using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IStokHesaplamaService
    {
        /// <summary>Bir hastanenin tüm reaktifleri için stok paneli üretir.</summary>
        Task<HastaneStokPaneliVm?> HesaplaHastaneAsync(
            int hospitalId,
            int hedefRunwayAy = 3,
            CancellationToken ct = default);

        /// <summary>Tek bir ürün için hesaplama (opsiyonel, modal/ajax için).</summary>
        Task<StokHesapSatirVm?> HesaplaUrunAsync(
            int hospitalId,
            int testId,
            int hedefRunwayAy = 3,
            CancellationToken ct = default);
    }
}
