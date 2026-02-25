using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class SutPointRowVm
    {
        public int TestID { get; set; }
        public string TestName { get; set; }
        public string? SutCode { get; set; }
        public decimal? Point { get; set; } // null olabilir
    }
}
