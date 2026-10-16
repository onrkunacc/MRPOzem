using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Helpers
{
    public static class UrunTipiTespitHelper
    {
        public static UrunTipi Tespit(string? productName, string? productType)
        {
            var pn = (productName ?? "").Trim().ToLowerInvariant();
            var pt = (productType ?? "").Trim().ToLowerInvariant();

            // 1) İSİM BAZLI (en spesifik) — kalibratör/kontrol ismiyle ayrılır
            //    "AFP Calibrator +" gibi Sarf tipinde olsa bile ismiyle yakalanır
            if (ContainsAny(pn, "calibrator", "kalibratör", "kalibrator", " kal.", " kal "))
                return UrunTipi.Kalibrator;

            if (ContainsAny(pn, "control", "kontrol", " kon.", " kon "))
                return UrunTipi.Kontrol;

            // 2) ★ PRODUCT TYPE'ta DIREKT "reaktif" varsa → Reaktif ★
            //    Bu kritik — "Reaktif", "Alinity Reaktif", "Architect Reaktif" vs. hepsini yakalar
            if (pt.Contains("reaktif"))
                return UrunTipi.Reaktif;

            // 3) Reaktif göstergesi olan diğer tip pattern'leri
            if (ContainsAny(pt,
                    "küçük kutu", "kucuk kutu",
                    "büyük kutu", "buyuk kutu",
                    "sigmastrong"))
                return UrunTipi.Reaktif;

            // Platform-sonlu reaktif desenleri ("Architect CC" ← CC standalone reaktif)
            if (pt.EndsWith(" cc") || pt == "cc" || pt.Contains(" cc ") || pt.EndsWith("/cc"))
                return UrunTipi.Reaktif;

            // İsimde "sigmastrong" varsa büyük ihtimal reaktif
            if (pn.Contains("sigmastrong"))
                return UrunTipi.Reaktif;

            // 4) SARF — "Cihaz Sarf", "Sarf", "Alinity Sarf", "Architect CC Sarf" vb.
            if (pt.Contains("sarf"))
                return UrunTipi.Sarf;

            // 5) Hiçbiri değilse
            return UrunTipi.Diger;
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (var n in needles)
                if (!string.IsNullOrEmpty(n) && haystack.Contains(n))
                    return true;
            return false;
        }

        /// <summary>Debug / log — tespitin "neden böyle" olduğunu açıklar.</summary>
        public static string TespitAciklamasi(string? productName, string? productType, UrunTipi tespit)
        {
            var pn = productName ?? "(boş)";
            var pt = productType ?? "(boş)";
            return $"Ad='{pn}', Tip='{pt}' → {tespit}";
        }
    }
}