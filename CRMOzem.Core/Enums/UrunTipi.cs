using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Enums
{
    public enum UrunTipi
    {
        Reaktif = 1,  // "Küçük Kutu", "CC", "sigmaSTRONG" — istatistiği var
        Kalibrator = 2,  // adında "Calibrator" geçen Sarf'lar
        Kontrol = 3,  // adında "Control" geçen Sarf'lar
        Sarf = 4,  // diğer Sarf'lar (wash, buffer, vb.)
        Diger = 99
    }
}
