using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class BirimOzetSatir
    {
        public string BirimKodu { get; set; } = string.Empty;
        public string BirimAdi { get; set; } = string.Empty;
        public decimal PersonelTRY { get; set; }
        public decimal EkGiderTRY { get; set; }
        public decimal ToplamSUT { get; set; }
        public decimal ToplamMaliyet { get; set; }
    }
}
