using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Enums
{
    public enum DogrulamaRengi
    {
        Yesil = 1,  // ±%10 içinde — sapma makul
        Sari = 2,  // ±%10-30 arasında — gözden geçir
        Kirmizi = 3,  // ±%30 üzerinde — mutlaka müdahale
        VeriYok = 0   // istatistik yetersiz, karar verilemiyor
    }
}
