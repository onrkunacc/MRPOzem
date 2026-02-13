using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class IstatistikDashboardVm
    {
        public int Yil { get; set; }
        public List<int> SeciliAylar { get; set; } = new();
        public List<KurumRowVm> Kurumler { get; set; } = new();
    }
}
