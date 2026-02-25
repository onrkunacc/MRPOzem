using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class MonthlyVolumeEntryVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }

        public List<MonthlyVolumeRowVm> Rows { get; set; } = new();
    }
}
