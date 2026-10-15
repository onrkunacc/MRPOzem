using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IKotaHareketService
    {
        Task<KotaDetayVm?> GetDetayAsync(int kotaId, CancellationToken ct = default);
        Task<KotaHareketEditVm?> GetHareketAsync(int hareketId, CancellationToken ct = default);
        Task<KotaHareket> CreateAsync(KotaHareketCreateVm vm, string? userId, CancellationToken ct = default);
        Task<KotaHareket> UpdateAsync(KotaHareketEditVm vm, CancellationToken ct = default);
        Task<bool> DeleteAsync(int hareketId, CancellationToken ct = default);
    }
}
