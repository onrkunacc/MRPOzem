using System;

namespace CRMOzem.Business.Helpers
{
  
    public static class IzinHakedisHesaplayici
    {
        public const int ASGARI_GUN = 14;
        public const int ORTA_KIDEM_GUN = 20;
        public const int UST_KIDEM_GUN = 26;
        public const int YAS_ISTISNA_GUN = 20;

       
        public static int Hesapla(DateTime? iseGirisTarihi, DateTime? dogumTarihi, int yil)
        {
            if (!iseGirisTarihi.HasValue) return 0;

            var referans = new DateTime(yil, 12, 31);

            // Henüz işe girmemişse
            if (iseGirisTarihi.Value.Date > referans) return 0;

            var kidem = TamYilFarki(iseGirisTarihi.Value, referans);

            // İzin hakkı bir yılı doldurunca doğar
            if (kidem < 1) return 0;

            int gun =
                kidem <= 5 ? ASGARI_GUN :
                kidem < 15 ? ORTA_KIDEM_GUN :
                              UST_KIDEM_GUN;

            // 18 yaş ve altı / 50 yaş ve üstü → en az 20 gün
            if (dogumTarihi.HasValue)
            {
                var yas = TamYilFarki(dogumTarihi.Value, referans);
                if (yas <= 18 || yas >= 50)
                    gun = Math.Max(gun, YAS_ISTISNA_GUN);
            }

            return gun;
        }

        /// <summary>Belirtilen yıl sonunda çalışanın kıdemi (tam yıl).</summary>
        public static int KidemYili(DateTime? iseGirisTarihi, int yil)
        {
            if (!iseGirisTarihi.HasValue) return 0;

            var referans = new DateTime(yil, 12, 31);
            if (iseGirisTarihi.Value.Date > referans) return 0;

            return TamYilFarki(iseGirisTarihi.Value, referans);
        }

        /// <summary>Bugün itibarıyla kıdem (tam yıl).</summary>
        public static int GuncelKidem(DateTime? iseGirisTarihi)
        {
            if (!iseGirisTarihi.HasValue) return 0;
            if (iseGirisTarihi.Value.Date > DateTime.Today) return 0;
            return TamYilFarki(iseGirisTarihi.Value, DateTime.Today);
        }

        /// <summary>Bugün itibarıyla yaş.</summary>
        public static int GuncelYas(DateTime? dogumTarihi)
        {
            if (!dogumTarihi.HasValue) return 0;
            if (dogumTarihi.Value.Date > DateTime.Today) return 0;
            return TamYilFarki(dogumTarihi.Value, DateTime.Today);
        }

        /// <summary>Hakedişin hangi kurala göre belirlendiğini açıklar — ekranda gösterilir.</summary>
        public static string Aciklama(DateTime? iseGirisTarihi, DateTime? dogumTarihi, int yil)
        {
            if (!iseGirisTarihi.HasValue)
                return "İşe giriş tarihi girilmemiş — otomatik hesaplanamıyor.";

            var kidem = KidemYili(iseGirisTarihi, yil);

            if (kidem < 1)
                return $"{yil} sonunda kıdem 1 yıldan az — izin hakkı henüz doğmadı.";

            string kural =
                kidem <= 5 ? $"{kidem} yıl kıdem (1–5 yıl) → 14 gün" :
                kidem < 15 ? $"{kidem} yıl kıdem (5–15 yıl) → 20 gün" :
                             $"{kidem} yıl kıdem (15+ yıl) → 26 gün";

            if (dogumTarihi.HasValue)
            {
                var yas = TamYilFarki(dogumTarihi.Value, new DateTime(yil, 12, 31));
                if (yas <= 18)
                    kural += $" · {yas} yaş (18 ve altı) → en az 20 gün";
                else if (yas >= 50)
                    kural += $" · {yas} yaş (50 ve üstü) → en az 20 gün";
            }

            return kural;
        }

        /// <summary>
        /// İki tarih arasındaki tam yıl farkı.
        /// 15.06.2020 → 14.06.2026 = 5 yıl, 15.06.2026 = 6 yıl.
        /// </summary>
        private static int TamYilFarki(DateTime baslangic, DateTime bitis)
        {
            var bas = baslangic.Date;
            var bit = bitis.Date;
            if (bit < bas) return 0;

            int yil = bit.Year - bas.Year;

            // Yıldönümü henüz gelmemişse bir eksilt
            if (bit.Month < bas.Month || (bit.Month == bas.Month && bit.Day < bas.Day))
                yil--;

            return Math.Max(0, yil);
        }
    }
}