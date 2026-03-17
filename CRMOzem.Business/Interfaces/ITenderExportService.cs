using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ITenderExportService
    {
        Task<byte[]> ExportExcelDailyAsync(DateTime date);
        Task<byte[]> ExportPdfDailyAsync(DateTime date);
    }
}
