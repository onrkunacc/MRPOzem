using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ILojistikDashboardService
    {
        Task<LojistikDashboardVm> GetDashboardAsync(CancellationToken ct);
    }
}
