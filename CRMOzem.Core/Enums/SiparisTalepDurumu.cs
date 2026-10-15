using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Enums
{
    public enum SiparisTalepDurumu
    {
        Beklemede = 0,  // hastaneden geldi, lojistik henüz bakmadı
        OnaylandiAynen = 1,  // lojistik "istenen miktar doğru" dedi
        OnaylandiDuzeltildi = 2, // lojistik miktarı değiştirdi (örn. 3 kutu istenmişti → 2 kutu onaylandı)
        Reddedildi = 3,  // iptal/stokta var vb.
        Gonderildi = 4   // Gönderim (MonthlyShipment) kaydı oluşturuldu
    }
}
