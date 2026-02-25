using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IHospitalTestSetService
    {
        Task<HospitalTestSetVm> GetAsync(int hospitalId);
        Task ApplyStandardSetAsync(int hospitalId);
        Task SetAllActiveAsync(int hospitalId, bool isActive);
        Task UpdateActiveFlagsAsync(int hospitalId, Dictionary<int, bool> activeByTestId);
    }
}
