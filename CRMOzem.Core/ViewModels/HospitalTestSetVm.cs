using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HospitalTestSetVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public List<HospitalTestSetRowVm> Rows { get; set; } = new();
    }

    public class HospitalTestSetSaveVm
    {
        public int HospitalId { get; set; }
        public List<HospitalTestSetSaveRowVm> Rows { get; set; } = new();
    }

    public class HospitalTestSetSaveRowVm
    {
        public int TestId { get; set; }
        public bool IsActive { get; set; }
    }
}
