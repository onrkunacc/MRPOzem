using CRMOzem.Core.Enums;

namespace CRMOzem.Core.ViewModels
{
    // ═══════════════════════════════════════════════════════════
    // FİLTRE
    // ═══════════════════════════════════════════════════════════
    public class KotaDashboardFilterVm
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Bolge { get; set; }
        public KotaDurum? Durum { get; set; }
        public TedarikciGrup? TedarikciGrup { get; set; }
        public int? KotaId { get; set; }

        public bool IsFiltered =>
            StartDate.HasValue || EndDate.HasValue ||
            !string.IsNullOrWhiteSpace(Bolge) ||
            Durum.HasValue || TedarikciGrup.HasValue || KotaId.HasValue;

        public string PeriyotText
        {
            get
            {
                if (StartDate.HasValue && EndDate.HasValue)
                    return $"{StartDate.Value:dd.MM.yyyy} - {EndDate.Value:dd.MM.yyyy}";
                if (StartDate.HasValue) return $"{StartDate.Value:dd.MM.yyyy}'den itibaren";
                if (EndDate.HasValue) return $"{EndDate.Value:dd.MM.yyyy} tarihine kadar";
                return "Tüm Zamanlar";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ANA VM
    // ═══════════════════════════════════════════════════════════
    public class KotaDashboardVm
    {
        public KotaDashboardFilterVm Filter { get; set; } = new();

        public List<string> MevcutBolgeler { get; set; } = new();
        public List<KotaOptionVm> MevcutKotalar { get; set; } = new();

        // ÜST KPI
        public int ToplamKotaSayisi { get; set; }
        public int AktifKotaSayisi { get; set; }
        public int OnaydaBekleyenSayisi { get; set; }
        public int YeniBaslayacakSayisi { get; set; }
        public int KapananSayisi { get; set; }

        public decimal ToplamProformaUsd { get; set; }
        public decimal ToplamGiderUsd { get; set; }
        public decimal ToplamGelirUsd { get; set; }
        public decimal KalanUsd => ToplamProformaUsd - ToplamGiderUsd;
        public decimal KarZararUsd => ToplamGelirUsd - ToplamGiderUsd;

        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal DigerGiderUsd { get; set; }

        // GRAFİK VERİLERİ
        public List<AylikTrendVm> AylikTrend { get; set; } = new();
        public List<KotaKullanimVm> KotaKullanimlari { get; set; } = new();
        public List<BolgeOzetVm> BolgeOzet { get; set; } = new();
        public List<YillikOzetVm> YillikOzet { get; set; } = new();
        public List<EnYukluKotaVm> EnYukluKotalar { get; set; } = new();

        // ★ YENİ ★ — KOTA SATIRLARI (ana tablo için)
        public List<KotaRowVm> KotaSatirlari { get; set; } = new();

        // Karşılaştırma
        public decimal BuAyGiderUsd { get; set; }
        public decimal GecenAyGiderUsd { get; set; }
        public decimal BuAyGelirUsd { get; set; }
        public decimal GecenAyGelirUsd { get; set; }

        public decimal? BuAyGiderDegisim
        {
            get
            {
                if (GecenAyGiderUsd <= 0) return null;
                return Math.Round(((BuAyGiderUsd - GecenAyGiderUsd) / GecenAyGiderUsd) * 100, 1);
            }
        }

        public decimal? BuAyGelirDegisim
        {
            get
            {
                if (GecenAyGelirUsd <= 0) return null;
                return Math.Round(((BuAyGelirUsd - GecenAyGelirUsd) / GecenAyGelirUsd) * 100, 1);
            }
        }

        public decimal? GuncelUsdKur { get; set; }
        public DateTime? GuncelKurTarihi { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // ★ YENİ ★  KOTA SATIR VM — Dashboard & Index tablosu için
    // ═══════════════════════════════════════════════════════════
    public class KotaRowVm
    {
        public int KotaID { get; set; }
        public string? KotaKodu { get; set; }
        public string IsAdi { get; set; } = "";
        public string? Sorumlu { get; set; }
        public string? Bolge { get; set; }

        public decimal ProformaUsd { get; set; }

        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal DigerGiderUsd { get; set; }
        public decimal ToplamGiderUsd => AbbottGiderUsd + ThirdPartyGiderUsd + DigerGiderUsd;

        public decimal GelirUsd { get; set; }

        /// <summary>Brüt Fark = Gelir - Toplam Gider  (pozitif → kar, negatif → zarar)</summary>
        public decimal BrutFarkUsd => GelirUsd - ToplamGiderUsd;

        /// <summary>Brüt Fark Yüzdesi — proforma'ya göre</summary>
        public decimal BrutFarkYuzde => ProformaUsd > 0
            ? Math.Round((BrutFarkUsd / ProformaUsd) * 100, 2)
            : 0;

        /// <summary>Kullanım = Toplam Gider / Proforma × 100</summary>
        public decimal KullanimYuzde => ProformaUsd > 0
            ? Math.Round((ToplamGiderUsd / ProformaUsd) * 100, 2)
            : 0;

        public KotaDurum Durum { get; set; }
        public DateTime? BaslamaTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
    }

    public class KotaOptionVm
    {
        public int KotaID { get; set; }
        public string IsAdi { get; set; } = "";
        public string? KotaKodu { get; set; }
    }

    public class AylikTrendVm
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string AyLabel { get; set; } = "";
        public decimal GiderUsd { get; set; }
        public decimal GelirUsd { get; set; }
        public decimal KarZararUsd => GelirUsd - GiderUsd;
    }

    public class KotaKullanimVm
    {
        public int KotaID { get; set; }
        public string IsAdi { get; set; } = "";
        public string? KotaKodu { get; set; }
        public decimal ProformaUsd { get; set; }
        public decimal GiderUsd { get; set; }
        public decimal Yuzde { get; set; }
        public KotaDurum Durum { get; set; }
    }

    public class BolgeOzetVm
    {
        public string Bolge { get; set; } = "";
        public int KotaSayisi { get; set; }
        public decimal ToplamProformaUsd { get; set; }
        public decimal ToplamGiderUsd { get; set; }
    }

    public class YillikOzetVm
    {
        public int Yil { get; set; }
        public decimal GiderUsd { get; set; }
        public decimal GelirUsd { get; set; }
        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal DigerGiderUsd { get; set; }
        public decimal KarZararUsd => GelirUsd - GiderUsd;
    }

    public class EnYukluKotaVm
    {
        public int KotaID { get; set; }
        public string IsAdi { get; set; } = "";
        public string? KotaKodu { get; set; }
        public string? Bolge { get; set; }
        public decimal GiderUsd { get; set; }
        public decimal ProformaUsd { get; set; }
        public decimal Yuzde { get; set; }
        public KotaDurum Durum { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // ★ YENİ ★  KOTA DETAY MODAL VM — modal'da gösterilen deep-dive
    // ═══════════════════════════════════════════════════════════
    public class KotaDetayModalVm
    {
        public int KotaID { get; set; }
        public string? KotaKodu { get; set; }
        public string IsAdi { get; set; } = "";
        public string? Sorumlu { get; set; }
        public string? Bolge { get; set; }
        public DateTime? BaslamaTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }

        public decimal ProformaUsd { get; set; }
        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal DigerGiderUsd { get; set; }
        public decimal ToplamGiderUsd => AbbottGiderUsd + ThirdPartyGiderUsd + DigerGiderUsd;
        public decimal GelirUsd { get; set; }
        public decimal BrutFarkUsd => GelirUsd - ToplamGiderUsd;
        public decimal KullanimYuzde => ProformaUsd > 0
            ? Math.Round((ToplamGiderUsd / ProformaUsd) * 100, 2)
            : 0;

        public KotaDurum Durum { get; set; }
        public List<AylikTrendVm> AylikTrend { get; set; } = new();
    }
}