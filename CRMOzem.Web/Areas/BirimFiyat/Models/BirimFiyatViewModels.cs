using CRMOzem.Core.DTOs;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Web.Areas.BirimFiyat.Models
{
    public class BirimFiyatIndexVm
    {
        /// <summary>Kök klasörler — alt klasörleri AltKlasorler üzerinden gezilir.</summary>
        public List<BirimFiyatKlasor> KlasorAgaci { get; set; } = new();

        /// <summary>Seçili klasördeki (veya aramaya uyan) karşılaştırmalar.</summary>
        public List<BirimFiyatKarsilastirma> Karsilastirmalar { get; set; } = new();

        public int? AktifKlasorId { get; set; }
        public BirimFiyatKlasor? AktifKlasor { get; set; }

        /// <summary>Kökten aktif klasöre kadar olan yol.</summary>
        public List<BirimFiyatKlasor> Yol { get; set; } = new();

        public string? Arama { get; set; }

        /// <summary>Taşıma ve üst klasör seçimi için düz liste.</summary>
        public List<SelectListItem> KlasorSecenekleri { get; set; } = new();

        // ── Üst şerit özetleri ───────────────────────────────────
        public int ToplamKarsilastirma { get; set; }
        public int HesaplananSayisi { get; set; }
        public int DikkatGerektirenToplam { get; set; }
        public decimal ToplamFarkTl { get; set; }

        public bool AramaModu => !string.IsNullOrWhiteSpace(Arama);
    }

    // ═══════════════════════════════════════════════════════════════
    //  KARŞILAŞTIRMA FORMU
    // ═══════════════════════════════════════════════════════════════

    public class KarsilastirmaFormVm
    {
        public BirimFiyatKarsilastirma Karsilastirma { get; set; } = new();

        public List<SelectListItem> Klasorler { get; set; } = new();
        public List<SelectListItem> Hastaneler { get; set; } = new();

        public bool Yeni => Karsilastirma.Id == 0;

        public string Baslik => Yeni ? "Yeni Karşılaştırma" : "Karşılaştırmayı Düzenle";
    }

    // ═══════════════════════════════════════════════════════════════
    //  DETAY / SONUÇ EKRANI
    // ═══════════════════════════════════════════════════════════════

    public class KarsilastirmaDetayVm
    {
        public BirimFiyatKarsilastirma Karsilastirma { get; set; } = new();

        public List<BirimFiyatSonuc> Sonuclar { get; set; } = new();

        // ── Filtreler ────────────────────────────────────────────
        public SonucDurumu? Durum { get; set; }
        public bool SadeceDikkat { get; set; }
        public string? Arama { get; set; }

        // ── Dönem bazlı özet (grafik için) ───────────────────────
        public List<DonemOzetVm> DonemOzetleri { get; set; } = new();

        /// <summary>En yüksek sapmalı ilk 5 ürün — üst şeritte gösterilir.</summary>
        public List<BirimFiyatSonuc> EnBuyukSapmalar { get; set; } = new();

        public List<string> Uyarilar { get; set; } = new();

        // ── Kolay erişim ─────────────────────────────────────────
        public string TedarikciEtiket => string.IsNullOrWhiteSpace(Karsilastirma.TedarikciEtiket)
            ? "Tedarikçi" : Karsilastirma.TedarikciEtiket;

        public string BizEtiket => string.IsNullOrWhiteSpace(Karsilastirma.BizEtiket)
            ? "Biz" : Karsilastirma.BizEtiket;

        public bool HesaplanabilirMi =>
            Karsilastirma.TedarikciSatirSayisi > 0 && Karsilastirma.BizSatirSayisi > 0;

        public bool SonucVarMi => Karsilastirma.Durum == KarsilastirmaDurumu.Hesaplandi;

        public int FiltreliToplam => Sonuclar.Count;
    }

    public class DonemOzetVm
    {
        public DateTime? Donem { get; set; }
        public int Adet { get; set; }
        public int DikkatAdedi { get; set; }
        public decimal ToplamFarkTl { get; set; }
        public decimal OrtalamaSapmaYuzde { get; set; }

        public string DonemAdi => Donem.HasValue
            ? Donem.Value.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))
            : "Tarihsiz";
    }

    // ═══════════════════════════════════════════════════════════════
    //  YÜKLEME SİHİRBAZI
    // ═══════════════════════════════════════════════════════════════

    public class YuklemeVm
    {
        public BirimFiyatKarsilastirma Karsilastirma { get; set; } = new();

        public KarsilastirmaTarafi Taraf { get; set; }

        public ExcelOnizlemeDto Onizleme { get; set; } = new();

        public KolonEslesmeDto Eslesme { get; set; } = new();

        /// <summary>Ekranda gösterilecek taraf adı (Abbott / ÖZEM).</summary>
        public string TarafAdi => Taraf == KarsilastirmaTarafi.Tedarikci
            ? (string.IsNullOrWhiteSpace(Karsilastirma.TedarikciEtiket) ? "Tedarikçi" : Karsilastirma.TedarikciEtiket)
            : (string.IsNullOrWhiteSpace(Karsilastirma.BizEtiket) ? "Biz" : Karsilastirma.BizEtiket);

        public string ParaBirimi => Taraf == KarsilastirmaTarafi.Tedarikci
            ? Karsilastirma.TedarikciParaBirimi
            : Karsilastirma.BizParaBirimi;

        /// <summary>Kolon seçim dropdown'ları için başlık listesi.</summary>
        public List<SelectListItem> KolonSecenekleri(int seciliIndex)
        {
            var liste = new List<SelectListItem>
            {
                new SelectListItem("— Seçilmedi —", "-1", seciliIndex < 0)
            };

            for (var i = 0; i < Onizleme.Basliklar.Count; i++)
            {
                liste.Add(new SelectListItem(
                    $"{HarfKolon(i)} · {Onizleme.Basliklar[i]}",
                    i.ToString(),
                    i == seciliIndex));
            }

            return liste;
        }

        /// <summary>0 → A, 1 → B, 26 → AA</summary>
        public static string HarfKolon(int index)
        {
            var s = string.Empty;
            var n = index;

            do
            {
                s = (char)('A' + (n % 26)) + s;
                n = n / 26 - 1;
            } while (n >= 0);

            return s;
        }
    }

    /// <summary>Kolon eşleştirme formundan gelen POST verisi.</summary>
    public class EslestirmeOnayVm
    {
        public int KarsilastirmaId { get; set; }
        public KarsilastirmaTarafi Taraf { get; set; }

        public string? SayfaAdi { get; set; }
        public int BaslikSatiri { get; set; } = 1;

        public int ReferansKolon { get; set; } = -1;
        public int UrunAdiKolon { get; set; } = -1;
        public int BirimFiyatKolon { get; set; } = -1;
        public int TarihKolon { get; set; } = -1;
        public int MiktarKolon { get; set; } = -1;
        public int FaturaNoKolon { get; set; } = -1;
        public int KurKolon { get; set; } = -1;
        public int BirimKolon { get; set; } = -1;

        public KolonEslesmeDto ToDto() => new()
        {
            SayfaAdi = SayfaAdi,
            BaslikSatiri = BaslikSatiri < 1 ? 1 : BaslikSatiri,
            ReferansKolon = ReferansKolon,
            UrunAdiKolon = UrunAdiKolon,
            BirimFiyatKolon = BirimFiyatKolon,
            TarihKolon = TarihKolon,
            MiktarKolon = MiktarKolon,
            FaturaNoKolon = FaturaNoKolon,
            KurKolon = KurKolon,
            BirimKolon = BirimKolon
        };
    }
}
