using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Enums
{
    public enum StokDurumu
    {
        VeriYok = 0,   // istatistik veya sayım yetersiz
        Kritik = 1,   // < 30 gün  veya ≤ 0 stok
        Dusuk = 2,   // 30-60 gün
        Izlemede = 3,   // 60-90 gün
        Saglikli = 4,   // ≥ 90 gün
        Durgun = 5    // kullanım yok, stok var — satış olmayan ürün
    }
}
