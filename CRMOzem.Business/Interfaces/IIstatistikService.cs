using CRMOzem.Core.DTOs.Istatistik;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IIstatistikService
    {
        Task<IstatistikDashboardVm> GetDashboardAsync(int yil, int[] aylar);
        Task SetGeldiMiAsync(SetGeldiMiRequest req);
        Task SetNotAsync(SetNotRequest req);
    }
}
