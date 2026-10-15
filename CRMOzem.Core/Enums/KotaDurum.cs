using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Enums
{
    public enum KotaDurum
    {
        YeniBaslayacak = 0,  // Henüz başlamamış 
        Onayda = 1,          // Onay bekleniyor
        Bekleyen = 2,        // Sorumlu bir kişide
        Aktif = 3,           // Devam ediyor
        Kapandi = 4          // Süresi bitti
    }
}
