using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class UsageTemplateFormVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public DateTime Month { get; set; }
        public string MonthLabel { get; set; } = "";

        public List<UsageTemplateRowVm> Rows { get; set; } = new();

        public bool HasExistingData => Rows.Any(r => r.MevcutAdet > 0);
        public int ExistingTestCount => Rows.Count(r => r.MevcutAdet > 0);
        public int ExistingTotalAmount => Rows.Sum(r => r.MevcutAdet);
    }

    public class UsageTemplateRowVm
    {
        public int TestID { get; set; }
        public string TestName { get; set; } = "";
        public string? SutCode { get; set; }
        public TestGirisTipi GirisTipi { get; set; }

        public int MevcutAdet { get; set; }
    }

    public class UsageTemplateSaveVm
    {
        public int HospitalID { get; set; }
        public DateTime Month { get; set; }

        /// <summary>"add", "overwrite", "skip"</summary>
        public string ConflictMode { get; set; } = "add";

        public List<UsageTemplateSaveRowVm> Rows { get; set; } = new();
    }

    public class UsageTemplateSaveRowVm
    {
        public int TestID { get; set; }
        public int? Amount { get; set; }
    }

    public class UsageSaveResult
    {
        public bool Success { get; set; }
        public int EklenenSatir { get; set; }
        public int GuncellenenSatir { get; set; }
        public int AtlananSatir { get; set; }
        public int ToplamAdet { get; set; }
        public string? ErrorMessage { get; set; }
    }
}