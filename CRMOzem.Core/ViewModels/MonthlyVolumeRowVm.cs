using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class MonthlyVolumeRowVm
    {
        public int Sira { get; set; }
        public int TestID { get; set; }
        public string TestName { get; set; }
        public string? SutCode { get; set; }
        public int Count { get; set; }
    }
}
