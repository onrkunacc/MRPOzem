using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class IhaleListeSatir
    {
        public Ihale Ihale { get; set; } = new();

        // ── Kalemler ────────────────────────────────────────────────
        public int KalemAdedi { get; set; }
        public decimal TestMiktari { get; set; }
        public decimal ToplamSutPuani { get; set; }
        public decimal ToplamSutFiyat { get; set; }

        // ── Giderler ────────────────────────────────────────────────
        public int PersonelAdedi { get; set; }
        public decimal PersonelTRY { get; set; }
        public int EkGiderAdedi { get; set; }
        public decimal EkGiderTRY { get; set; }

        /// <summary>Personel + Ek Gider + Genel Gider + SUT tutarı.</summary>
        public decimal GenelToplam { get; set; }

        // ── Kısımlar ────────────────────────────────────────────────

        /// <summary>Veri girilmiş kısım numaraları (artan sırada).</summary>
        public List<int> DoluKisimlar { get; set; } = new();

        /// <summary>Hiçbir kısma atanmamış kayıt sayısı.</summary>
        public int OrtakKayitAdedi { get; set; }

        // ── Türetilenler ────────────────────────────────────────────

        public int KisimSayisi => Math.Max(1, Ihale.KisimSayisi);

        public int DoluKisimAdedi => DoluKisimlar.Count(n => n >= 1 && n <= KisimSayisi);

        /// <summary>Şartnamede tanımlı olup henüz veri girilmemiş kısımlar.</summary>
        public List<int> BosKisimlar =>
            Enumerable.Range(1, KisimSayisi).Where(n => !DoluKisimlar.Contains(n)).ToList();

        /// <summary>Kısım sayısı düşürülmüş de veride kalmış numaralar.</summary>
        public List<int> TanimDisiKisimlar =>
            DoluKisimlar.Where(n => n < 1 || n > KisimSayisi).ToList();

        public bool CokKisimli => KisimSayisi > 1;

        public bool EksikKisimVar => CokKisimli && BosKisimlar.Count > 0;

        public int ToplamKayitAdedi => KalemAdedi + PersonelAdedi + EkGiderAdedi;

        public bool BosIhale => ToplamKayitAdedi == 0;

        public decimal MaliyetSutPuani =>
            ToplamSutPuani > 0 ? GenelToplam / ToplamSutPuani : 0m;
    }
}
