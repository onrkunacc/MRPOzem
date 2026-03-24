using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IIhaleService
    {
        Task<List<Ihale>> GetAllAsync();
        Task<Ihale?> GetByIdAsync(int id);
        Task<int> CreateAsync(Ihale ihale);
        Task UpdateAsync(Ihale ihale);
        Task DeleteAsync(int id);
        Task<OzetViewModel> GetOzetAsync(int ihaleId);
        Task<List<Birim>> GetBirimlerAsync();
        Task<Birim> SaveBirimAsync(Birim birim);
        Task<PersonelGider> SavePersonelAsync(PersonelGider p);
        Task DeletePersonelAsync(int id);
        Task<EkGider> SaveEkGiderAsync(EkGider e);
        Task DeleteEkGiderAsync(int id);
        Task<IhaleKalem> SaveKalemAsync(IhaleKalem k);
        Task DeleteKalemAsync(int id);
    }
}
