using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IKotaService
    {
        Task<List<KotaListVm>> GetListAsync(KotaFilterVm filter, CancellationToken ct = default);
        Task<KotaEditVm?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Kota> CreateAsync(KotaCreateVm vm, string? userId, CancellationToken ct = default);
        Task<Kota> UpdateAsync(KotaEditVm vm, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<List<string>> GetBolgelerAsync(CancellationToken ct = default);
    }
}
