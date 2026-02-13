using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class AyDurumVm
    {
        public int Ay { get; set; }          // 1-12
        public bool GeldiMi { get; set; }
        public string? Not { get; set; }
        public string? UpdatedAtText { get; set; } // opsiyonel: UI’da göstermek istersen
    }
}
