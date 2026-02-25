using CRMOzem.Core.DTOs.Istatistik;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ISutComparisonService
    {
        Task<SutComparisonVm> BuildMonthlyAsync(int hospitalId, int year, int month, int ihaleYear, int currentYear);
    }
}
