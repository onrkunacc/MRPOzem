using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ITedarikciService
    {
        Task<List<TedarikciListVm>> GetAllAsync(bool onlyActive = false, CancellationToken ct = default);
        Task<TedarikciEditVm?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Tedarikci> CreateAsync(TedarikciCreateVm vm, CancellationToken ct = default);
        Task<Tedarikci> UpdateAsync(TedarikciEditVm vm, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<bool> ToggleActiveAsync(int id, CancellationToken ct = default);
    }
}
