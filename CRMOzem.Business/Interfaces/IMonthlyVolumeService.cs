using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IMonthlyVolumeService
    {
        Task<MonthlyVolumeEntryVm> GetEntryAsync(int hospitalId, int year, int month);
        Task SaveAsync(int hospitalId, int year, int month, Dictionary<int, int> countsByTestId);
        Task CopyPreviousMonthAsync(int hospitalId, int year, int month, bool overwriteExisting);
    }
}
