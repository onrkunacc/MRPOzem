using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class KisimOzetSatir
    {
        
        public int? KisimNo { get; set; }

     
        public bool TanimDisi { get; set; }

        public string Ad => KisimNo.HasValue ? $"{KisimNo}. Kısım" : "Ortak Giderler";

        public string KisaAd => KisimNo.HasValue ? $"K{KisimNo}" : "Ortak";

        // ── Tutarlar ────────────────────────────────────────────────
        public decimal PersonelTRY { get; set; }
        public decimal EkGiderTRY { get; set; }
        public decimal GenelGider { get; set; }
        public decimal ToplamSutFiyat { get; set; }

        /// <summary>Personel + Ek Gider + Genel Gider + SUT tutarı.</summary>
        public decimal ToplamMaliyet { get; set; }

        // ── Hacim ───────────────────────────────────────────────────
        public decimal ToplamSutPuani { get; set; }
        public decimal TestMiktari { get; set; }

        // ── Sayılar ─────────────────────────────────────────────────
        public int KalemAdedi { get; set; }
        public int PersonelAdedi { get; set; }
        public int EkGiderAdedi { get; set; }

        // ── Türetilenler ────────────────────────────────────────────
        public int KayitAdedi => KalemAdedi + PersonelAdedi + EkGiderAdedi;

        public bool KayitVar => KayitAdedi > 0;

        /// <summary>Bu kısım için maliyetin SUT puanı başına düşen değeri.</summary>
        public decimal MaliyetSutPuani => ToplamSutPuani > 0 ? ToplamMaliyet / ToplamSutPuani : 0m;

        /// <summary>Toplam maliyet içindeki payı (yüzde). Servis doldurur.</summary>
        public decimal PayYuzde { get; set; }
    }
}
