using CRMOzem.Core.ViewModels;

namespace CRMOzem.Business.Interfaces
{
    public interface IKotaDashboardService
    {
        Task<KotaDashboardVm> BuildAsync(KotaDashboardFilterVm? filter = null, CancellationToken ct = default);
        Task<KotaDetayModalVm?> GetKotaDetayAsync(int kotaId, CancellationToken ct = default);
    }
}