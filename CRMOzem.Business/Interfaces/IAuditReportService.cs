using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IAuditReportService
    {
        Task<(MemoryStream?, string?)> GenerateExcelReport(
            int? cariGrupId,
            List<int>? cariIds,
            string? il,
            string? ihaleNo,
            int? donem,
            int? baslangicAy,
            int? bitisAy);
        Task<(MemoryStream?, string?)> GeneratePdfReport(int? cariGrupId, List<int>? cariIds, string? il, string? ihaleNo, int? donem, int? baslangicAy, int? bitisAy);
        
    }
}
