using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class SutPointVm
    {
        public int Year { get; set; }
        public List<SutPointRowVm> Rows { get; set; } = new();
    }
}
