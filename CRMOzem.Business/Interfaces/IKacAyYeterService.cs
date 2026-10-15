using CRMOzem.Core.ViewModels;

namespace CRMOzem.Business.Interfaces
{
    public interface IKacAyYeterService
    {
       
        Task<KacAyYeterReportVm> BuildAsync(
            int hospitalId,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken ct = default);
    }
}