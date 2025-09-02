using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs
{
    public class HospitalTestBalanceDTO
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; }

        public int TestID { get; set; }
        public string TestName { get; set; }

        public int TotalSent { get; set; }
        public int TotalUsed { get; set; }
        public int Remaining => TotalSent - TotalUsed;
    }
}
