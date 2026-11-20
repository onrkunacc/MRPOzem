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
    //  Bütün miktarlar TEST adedidir. (Sayım, gönderim ve kullanım test olarak
    //  girilir; yalnızca hastane siparişi kutu olarak istenir.)
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
        Eriyor = 3,

        /// <summary>
        /// Fark 3 katı aşıyor: gerçek bir tüketim farkı değil, büyük olasılıkla
        /// kayıt sorunu (gönderim eksik girilmiş ya da gönderim ve kullanım
        /// farklı birimle — kutu / test — girilmiş).
        /// </summary>
        KontrolEdilmeli = 4
    }

    public class RaporHastaneSatirVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public DateTime? SonSayimTarihi { get; set; }

        /// <summary>Kullanım (istatistik) girilmiş son ay.</summary>
        public DateTime? KullanimSonAy { get; set; }

        public int UrunSayisi { get; set; }

        /// <summary>Bugünkü stok — test. Fiziki sayımı olmayan hastanede null.</summary>
        public decimal? StokTest { get; set; }

        /// <summary>Stoğu (sayımı) bilinen ürün sayısı.</summary>
        public int SayimliUrun { get; set; }

        /// <summary>Aylık ortalama tüketim — test.</summary>
        public decimal AylikTest { get; set; }

        /// <summary>Dönemde gönderilen — test.</summary>
        public decimal GonderilenTest { get; set; }

        /// <summary>Dönemde kullanılan — test.</summary>
        public decimal KullanilanTest { get; set; }

        public decimal FarkTest => GonderilenTest - KullanilanTest;

        /// <summary>Sayımlı ürünlerin aylık tüketimi — kaç ay yeter hesabı için.</summary>
        public decimal SayimliAylikTest { get; set; }

        /// <summary>Eldeki stok kaç ay yeter (sayım ya da tüketim yoksa null).</summary>
        public decimal? StokAy => StokTest.HasValue && SayimliAylikTest > 0
            ? Math.Max(0m, Math.Round(StokTest.Value / SayimliAylikTest, 1)) : null;

        public int KritikUrun { get; set; }

        public DengeDurumu Denge
        {
            get
            {
                if (KullanilanTest <= 0) return DengeDurumu.Bilinmiyor;
                var oran = GonderilenTest / KullanilanTest;
                if (oran > 3m || oran < 1m / 3m) return DengeDurumu.KontrolEdilmeli;
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

        /// <summary>Fiziki sayımı olan hastane sayısı.</summary>
        public int SayimliHastane => Hastaneler.Count(h => h.StokTest.HasValue);
        public int SayimsizHastane => Hastaneler.Count(h => !h.StokTest.HasValue);

        /// <summary>Yalnızca sayımı olan hastanelerin stoğu.</summary>
        public decimal ToplamStok => Hastaneler.Sum(h => h.StokTest ?? 0);
        public decimal ToplamGonderilen => Hastaneler.Sum(h => h.GonderilenTest);
        public decimal ToplamKullanilan => Hastaneler.Sum(h => h.KullanilanTest);
        public decimal ToplamAylik => Hastaneler.Sum(h => h.AylikTest);
        private decimal ToplamSayimliAylik => Hastaneler.Where(h => h.StokTest.HasValue).Sum(h => h.SayimliAylikTest);
        public decimal? ToplamStokAy => SayimliHastane > 0 && ToplamSayimliAylik > 0
            ? Math.Max(0m, Math.Round(ToplamStok / ToplamSayimliAylik, 1)) : null;

        public int Dengeli => Hastaneler.Count(h => h.Denge == DengeDurumu.Dengeli);
        public int Birikiyor => Hastaneler.Count(h => h.Denge == DengeDurumu.Birikiyor);
        public int Eriyor => Hastaneler.Count(h => h.Denge == DengeDurumu.Eriyor);
        public int Bilinmiyor => Hastaneler.Count(h => h.Denge == DengeDurumu.Bilinmiyor);
        public int KontrolEdilmeli => Hastaneler.Count(h => h.Denge == DengeDurumu.KontrolEdilmeli);
    }

    // ── Tek hastane ──────────────────────────────────────────────

    public class RaporAyVm
    {
        public DateTime Ay { get; set; }
        public decimal GonderilenTest { get; set; }
        public decimal KullanilanTest { get; set; }

        /// <summary>O ay için kullanım (istatistik) girişi yapılmış mı?</summary>
        public bool KullanimGirildi { get; set; }
    }

    public class RaporUrunSatirVm
    {
        public int TestID { get; set; }
        public string UrunAdi { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public string Birim { get; set; } = "test";

        /// <summary>Kayıtlar tutarsız (sayım + gönderim − kullanım eksi).</summary>
        public bool KayitTutarsiz { get; set; }

        /// <summary>Fiziki sayımı yoksa null.</summary>
        public decimal? StokTest { get; set; }
        public decimal AylikTest { get; set; }
        public decimal GonderilenTest { get; set; }
        public decimal KullanilanTest { get; set; }
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
        public decimal StokTest { get; set; }
        public decimal AylikTest { get; set; }
        public decimal? StokAy { get; set; }
        public StokDurumu Durum { get; set; }
        public DateTime? SonSayimTarihi { get; set; }
    }

    public class RaporUrunVm
    {
        public int? TestID { get; set; }
        public string? UrunAdi { get; set; }
        public string? ReferenceNumber { get; set; }
        public string Birim { get; set; } = "test";

        public List<RaporUrunSecenekVm> Urunler { get; set; } = new();
        public List<RaporUrunHastaneSatirVm> Hastaneler { get; set; } = new();

        public decimal ToplamStok => Hastaneler.Sum(h => h.StokTest);
        public decimal ToplamAylik => Hastaneler.Sum(h => h.AylikTest);
        public decimal? ToplamStokAy => ToplamAylik > 0 ? Math.Max(0m, Math.Round(ToplamStok / ToplamAylik, 1)) : null;
    }
}
