using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ISutPointService
    {
        Task<SutPointVm> GetAsync(int year);
        Task SaveAsync(int year, Dictionary<int, decimal?> pointsByTestId);
        Task CopyFromYearAsync(int fromYear, int toYear, bool overwrite);
    }
}
