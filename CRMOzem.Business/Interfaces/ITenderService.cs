using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ITenderService
    {
        Task<List<Tender>> GetAllAsync();

        Task<Tender> CreateAsync(Tender tender);
        Task<Tender> UpdateAsync(Tender tender);
        Task<Tender?> GetByIdAsync(int id);
        Task<bool> DeleteAsync(int id);

        Task<decimal> CalculateTotalAsync(int tenderId);
    }
}
