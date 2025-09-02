using CRMOzem.Core.DTOs;
using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IStatisticsService
    {
        // Gönderim işlemleri
        Task AddShipmentAsync(int hospitalId, int testId, DateTime month, int sentCount);
        Task<List<MonthlyShipment>> GetShipmentsAsync(int hospitalId, DateTime? startDate, DateTime? endDate);

        // Kullanım işlemleri
        Task AddUsageAsync(int hospitalId, int testId, DateTime month, int usedCount);
        Task<List<MonthlyStatistics>> GetUsagesAsync(int hospitalId, DateTime? startDate, DateTime? endDate);

        // Rapor
        Task<List<HospitalTestBalanceDTO>> GetHospitalTestBalanceAsync(int hospitalId, DateTime? startDate, DateTime? endDate);
    }
}
