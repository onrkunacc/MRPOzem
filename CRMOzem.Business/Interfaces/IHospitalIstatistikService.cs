using CRMOzem.Core.DTOs.Istatistik;

namespace CRMOzem.Business.Interfaces
{
    public interface IHospitalIstatistikService
    {
        Task<HospitalIstatistikDashboardVm> GetDashboardAsync(int yil, int[] aylar);
        Task SetGeldiMiAsync(SetHospitalGeldiMiRequest req);
        Task SetNotAsync(SetHospitalNotRequest req);
    }
}