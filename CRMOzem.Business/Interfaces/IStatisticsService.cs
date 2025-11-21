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
        Task AddShipmentAsync(int hospitalId, int testId, DateTime month, int sentCount, CancellationToken ct = default);
        Task<List<MonthlyShipment>> GetShipmentsAsync(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);

        // Kullanım işlemleri
        Task AddUsageAsync(int hospitalId, int testId, DateTime month, int usedCount, CancellationToken ct = default);
        Task<List<MonthlyStatistics>> GetUsagesAsync(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);

        // Rapor — Aylık bazda
        Task<List<HospitalTestBalanceDTO>> GetHospitalTestBalanceAsync(int hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
    }
}
