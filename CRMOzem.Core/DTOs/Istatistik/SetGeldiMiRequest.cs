using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class SetGeldiMiRequest
    {
        public int KurumId { get; set; }
        public int Yil { get; set; }
        public int Ay { get; set; }     // 1-12
        public bool GeldiMi { get; set; }
    }
}
