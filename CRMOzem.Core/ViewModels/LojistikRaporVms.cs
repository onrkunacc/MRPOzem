using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMOzem.Core.ViewModels
{
    // ═══════════════════════════════════════════════════════════════
    //  LOJİSTİK RAPORLAMA
    // ═══════════════════════════════════════════════════════════════
    //  Üç soruya cevap verir:
    //    1. Hangi hastanede ne kadar stok var, ne kadar gönderdik,
    //       ne kadar kullanıldı — rakamlar birbirini tutuyor mu?
    //    2. Bir hastanede ürün ürün durum ne?
    //    3. Bir ürün hangi hastanelerde, ne kadar var? ("nerede ne var")
    //
    //  Bütün miktarlar KUTU cinsindendir (kutuyla satılmayan üründe adet).
    //  Stok rakamı Stok Öneri ve Doğrulama ekranlarıyla aynı hesaptan gelir.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Gönderim ile kullanım birbirini tutuyor mu?</summary>
    public enum DengeDurumu
    {
        /// <summary>Karşılaştırılacak kullanım verisi yok.</summary>
        Bilinmiyor = 0,

        /// <summary>Gönderilen ≈ kullanılan (±%15).</summary>
        Dengeli = 1,

        /// <summary>Gönderilen kullanılandan belirgin fazla — stok birikiyor.</summary>
        Birikiyor = 2,

        /// <summary>Kullanılan gönderilenden belirgin fazla — stok eriyor.</summary>
        Eriyor = 3
    }

    public class RaporHastaneSatirVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public DateTime? SonSayimTarihi { get; set; }

        /// <summary>Kullanım (istatistik) girilmiş son ay.</summary>
        public DateTime? KullanimSonAy { get; set; }

        public int UrunSayisi { get; set; }

        /// <summary>Bugünkü tahmini stok — kutu.</summary>
        public decimal StokKutu { get; set; }

        /// <summary>Aylık ortalama tüketim — kutu.</summary>
        public decimal AylikKutu { get; set; }

        /// <summary>Dönemde gönderilen — kutu.</summary>
        public decimal GonderilenKutu { get; set; }

        /// <summary>Dönemde kullanılan — kutu.</summary>
        public decimal KullanilanKutu { get; set; }

        public decimal FarkKutu => GonderilenKutu - KullanilanKutu;

        /// <summary>Eldeki stok kaç ay yeter (tüketim yoksa null).</summary>
        public decimal? StokAy => AylikKutu > 0 ? Math.Max(0m, Math.Round(StokKutu / AylikKutu, 1)) : null;

        public int KritikUrun { get; set; }

        public DengeDurumu Denge
        {
            get
            {
                if (KullanilanKutu <= 0) return DengeDurumu.Bilinmiyor;
                var oran = GonderilenKutu / KullanilanKutu;
                if (oran > 1.15m) return DengeDurumu.Birikiyor;
                if (oran < 0.85m) return DengeDurumu.Eriyor;
                return DengeDurumu.Dengeli;
            }
        }
    }

    public class RaporIndexVm
    {
        /// <summary>Karşılaştırma dönemi — son kaç tam ay.</summary>
        public int DonemAy { get; set; } = 6;
        public DateTime DonemBas { get; set; }
        public DateTime DonemSon { get; set; }

        public List<RaporHastaneSatirVm> Hastaneler { get; set; } = new();

        public decimal ToplamStok => Hastaneler.Sum(h => h.StokKutu);
        public decimal ToplamGonderilen => Hastaneler.Sum(h => h.GonderilenKutu);
        public decimal ToplamKullanilan => Hastaneler.Sum(h => h.KullanilanKutu);
        public decimal ToplamAylik => Hastaneler.Sum(h => h.AylikKutu);
        public decimal? ToplamStokAy => ToplamAylik > 0 ? Math.Max(0m, Math.Round(ToplamStok / ToplamAylik, 1)) : null;

        public int Dengeli => Hastaneler.Count(h => h.Denge == DengeDurumu.Dengeli);
        public int Birikiyor => Hastaneler.Count(h => h.Denge == DengeDurumu.Birikiyor);
        public int Eriyor => Hastaneler.Count(h => h.Denge == DengeDurumu.Eriyor);
        public int Bilinmiyor => Hastaneler.Count(h => h.Denge == DengeDurumu.Bilinmiyor);
    }

    // ── Tek hastane ──────────────────────────────────────────────

    public class RaporAyVm
    {
        public DateTime Ay { get; set; }
        public decimal GonderilenKutu { get; set; }
        public decimal KullanilanKutu { get; set; }

        /// <summary>O ay için kullanım (istatistik) girişi yapılmış mı?</summary>
        public bool KullanimGirildi { get; set; }
    }

    public class RaporUrunSatirVm
    {
        public int TestID { get; set; }
        public string UrunAdi { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public string Birim { get; set; } = "kutu";

        public decimal StokKutu { get; set; }
        public decimal AylikKutu { get; set; }
        public decimal GonderilenKutu { get; set; }
        public decimal KullanilanKutu { get; set; }
        public decimal? StokAy { get; set; }
        public StokDurumu Durum { get; set; }

        /// <summary>Bu ürünün kullanımı takip ediliyor mu (reaktif)?</summary>
        public bool KullanimTakipli { get; set; }
    }

    public class RaporHastaneVm
    {
        public int DonemAy { get; set; } = 6;
        public RaporHastaneSatirVm Ozet { get; set; } = new();
        public List<RaporAyVm> Aylar { get; set; } = new();
        public List<RaporUrunSatirVm> Urunler { get; set; } = new();
    }

    // ── Tek ürün: nerede ne var ──────────────────────────────────

    public class RaporUrunSecenekVm
    {
        public int TestID { get; set; }
        public string Ad { get; set; } = "";
    }

    public class RaporUrunHastaneSatirVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public decimal StokKutu { get; set; }
        public decimal AylikKutu { get; set; }
        public decimal? StokAy { get; set; }
        public StokDurumu Durum { get; set; }
        public DateTime? SonSayimTarihi { get; set; }
    }

    public class RaporUrunVm
    {
        public int? TestID { get; set; }
        public string? UrunAdi { get; set; }
        public string? ReferenceNumber { get; set; }
        public string Birim { get; set; } = "kutu";

        public List<RaporUrunSecenekVm> Urunler { get; set; } = new();
        public List<RaporUrunHastaneSatirVm> Hastaneler { get; set; } = new();

        public decimal ToplamStok => Hastaneler.Sum(h => h.StokKutu);
        public decimal ToplamAylik => Hastaneler.Sum(h => h.AylikKutu);
        public decimal? ToplamStokAy => ToplamAylik > 0 ? Math.Max(0m, Math.Round(ToplamStok / ToplamAylik, 1)) : null;
    }
}
