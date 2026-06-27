using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IUsageTemplateService
    {
        Task<UsageTemplateFormVm> BuildFormAsync(int hospitalId, DateTime month, CancellationToken ct = default);
        Task<UsageSaveResult> SaveAsync(UsageTemplateSaveVm model, CancellationToken ct = default);
    }
}
