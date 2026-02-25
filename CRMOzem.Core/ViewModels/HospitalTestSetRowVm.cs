using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HospitalTestSetRowVm
    {
        public int TestID { get; set; }
        public string TestName { get; set; }
        public string? SutCode { get; set; }

        public bool IsLinked { get; set; }  // template var mı?
        public bool IsActive { get; set; }  // checkbox
    }
}
