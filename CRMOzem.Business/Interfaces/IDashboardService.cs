using CRMOzem.Core.DTOs.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IDashboardService
    {
        Task<KpiDto> GetKpisAsync(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
        Task<List<TrendPointDto>> GetTrendsAsync(int? hospitalId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
        Task<(List<TopItemDto> shipments, List<TopItemDto> usage)> GetTopTestsAsync(int? hospitalId, DateTime? startDate, DateTime? endDate, int take = 10, CancellationToken ct = default);
        Task<List<ByHospitalDto>> GetByHospitalAsync(DateTime? startDate, DateTime? endDate, int take = 12, CancellationToken ct = default);
    }
}
