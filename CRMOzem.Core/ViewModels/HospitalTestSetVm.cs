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
        public string HospitalName { get; set; }
        public List<HospitalTestSetRowVm> Rows { get; set; } = new();
    }
}
