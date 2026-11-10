using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class OzetViewModel
    {
        public Ihale Ihale { get; set; } = new();

        public decimal PersonelToplam { get; set; }
        public decimal EkGiderToplam { get; set; }
        public decimal GenelGider { get; set; }
        public decimal ToplamSutFiyat { get; set; }
        public decimal GenelToplam { get; set; }
        public decimal ToplamMaliyetSutPuani { get; set; }
        public decimal ToplamSutPuani { get; set; }
        public List<BirimOzetSatir> BirimOzetler { get; set; } = new();

        
        public List<KisimOzetSatir> KisimOzetler { get; set; } = new();

   
        public KisimOzetSatir OrtakOzet { get; set; } = new();

    
        public bool KisimKirilimiVar =>
            Ihale.KisimSayisi > 1 || KisimOzetler.Any(x => x.KayitVar);

        public bool TanimDisiKisimVar => KisimOzetler.Any(x => x.TanimDisi && x.KayitVar);

        /// <summary>Kısımlara dağıtılmamış ortak gider var mı?</summary>
        public bool OrtakGiderVar => OrtakOzet.KayitVar;

        /// <summary>
        /// Kısımların toplamı + ortak = GenelToplam olmalı.
        /// Ekranda mutabakat kontrolü için.
        /// </summary>
        public decimal KisimlarToplami =>
            KisimOzetler.Sum(x => x.ToplamMaliyet) + OrtakOzet.ToplamMaliyet;
    }
}
