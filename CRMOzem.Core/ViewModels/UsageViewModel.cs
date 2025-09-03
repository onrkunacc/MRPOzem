using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class UsageViewModel
    {
        public int Id { get; set; }

        public int HospitalID { get; set; }
        public string HospitalName { get; set; }

        public int TestID { get; set; }
        public string TestName { get; set; }

        public DateTime Month { get; set; }

        public int TestCount { get; set; }
    }
}
