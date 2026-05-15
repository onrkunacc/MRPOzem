using CRMOzem.Core.ViewModels.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardVm> GetDashboardAsync(string periyot, CancellationToken ct = default);
    }
}
