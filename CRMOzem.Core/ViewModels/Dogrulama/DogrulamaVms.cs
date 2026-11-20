using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMOzem.Core.ViewModels.Dogrulama
{
    // ═══════════════════════════════════════════════════════════
    //  LİSTE
    // ═══════════════════════════════════════════════════════════
    public class DogrulamaIndexVm
    {
        public List<DogrulamaListSatirVm> Satirlar { get; set; } = new();
        public DogrulamaFiltreVm Filtre { get; set; } = new();

        // ── Durum sayıları (süzgeçten bağımsız) ──
        public int ToplamBekleyen { get; set; }
        public int ToplamOnaylananAynen { get; set; }
        public int ToplamOnaylananDuzeltildi { get; set; }
        public int ToplamReddedilen { get; set; }
        public int ToplamOnaylanan => ToplamOnaylananAynen + ToplamOnaylananDuzeltildi;

        public int ToplamKirmizi { get; set; }   // en az bir sorunlu kalemi olan bekleyen talep
        public int ToplamSari { get; set; }

        // ── Bekleyen taleplerin kalem bazlı özeti ──
        public int BekleyenKalem { get; set; }
        public int UygunKalem { get; set; }
        public int InceleKalem { get; set; }
        public int SorunluKalem { get; set; }
        public int VeriYokKalem { get; set; }
        public int EslesmeyenKalem { get; set; }

        /// <summary>Tüm kalemleri uygun çıkan bekleyen talep sayısı — hemen onaylanabilir.</summary>
        public int HemenOnaylanabilir { get; set; }

        /// <summary>Sistemin önerdiğinden fazla istenen toplam kutu (bekleyenlerde).</summary>
        public decimal FazlaIstenenKutu { get; set; }

        /// <summary>Sistemin önerdiğinden az istenen toplam kutu (bekleyenlerde).</summary>
        public decimal EksikIstenenKutu { get; set; }

        /// <summary>Katalogla eşleşmeyen ürünler — en çok geçenler.</summary>
        public List<EslesmeyenUrunVm> EslesmeyenUrunler { get; set; } = new();

        /// <summary>Hakkında hüküm verilebilen (stok ve tüketimi bilinen) bekleyen kalem.</summary>
        public int DegerlendirilenKalem => UygunKalem + InceleKalem + SorunluKalem;

        /// <summary>
        /// Uyum oranı: TÜM bekleyen taleplerin (ekrandaki sayfanın değil)
        /// değerlendirilebilen kalemleri içinde "uygun" çıkanların payı.
        /// Veri olmadığı için değerlendirilemeyen kalemler paydaya girmez —
        /// yoksa eşleşmeyen ürünler oranı haksız yere sıfıra çeker.
        /// </summary>
        public int UygunYuzde => DegerlendirilenKalem > 0
            ? (int)Math.Round(UygunKalem * 100m / DegerlendirilenKalem)
            : 0;

        /// <summary>15 günden eski bekleyen talep — büyük olasılıkla çoktan gönderilmiş.</summary>
        public int EskiBekleyen { get; set; }

        // ── Sayfalama ──
        public int Sayfa { get; set; } = 1;
        public int SayfaBoyu { get; set; } = 25;
        public int ToplamSatir { get; set; }
        public int SayfaSayisi => Math.Max(1, (int)Math.Ceiling(ToplamSatir / (double)Math.Max(1, SayfaBoyu)));
    }

    public class EslesmeyenUrunVm
    {
        public string ProductName { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public int KalemSayisi { get; set; }
        public int HastaneSayisi { get; set; }
    }

    public class DogrulamaListSatirVm
    {
        public int TalepID { get; set; }
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public string? HedefDonem { get; set; }         // "2026-10" vb.
        public DateTime TalepTarihi { get; set; }
        public string? ExternalRefNo { get; set; }      // sipariş yazılımındaki numara
        public SiparisTalepDurumu Durum { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }

        public int KalemSayisi { get; set; }
        public int ToplamTalepTest { get; set; }
        public int ToplamOneriTest { get; set; }

        // ── Hüküm dağılımı ──
        public DogrulamaRengi EnKotuRenk { get; set; } = DogrulamaRengi.VeriYok;
        public int KirmiziSayisi { get; set; }
        public int SariSayisi { get; set; }
        public int YesilSayisi { get; set; }
        public int VeriYokSayisi { get; set; }
        public int EslesmeyenSayisi { get; set; }

        public string? HastaneNotu { get; set; }

        /// <summary>Hastanenin veri güveni — bekleyen talepte hüküm ne kadar sağlam.</summary>
        public VeriGuveni Guven { get; set; }

        public int BekleyenGun => (int)(DateTime.Today - TalepTarihi.Date).TotalDays;

        /// <summary>Tüm kalemler uygun — bakmadan onaylanabilir.</summary>
        public bool TumuUygun => KalemSayisi > 0 && YesilSayisi == KalemSayisi;

        /// <summary>Tek cümle: bu talepte ne yapmalıyım?</summary>
        public string Ozet
        {
            get
            {
                if (KalemSayisi == 0) return "Kalem yok";
                if (TumuUygun) return "Tüm kalemler uygun";

                var p = new List<string>();
                if (KirmiziSayisi > 0) p.Add($"{KirmiziSayisi} sorunlu");
                if (SariSayisi > 0) p.Add($"{SariSayisi} incelenecek");
                if (VeriYokSayisi > 0) p.Add($"{VeriYokSayisi} veri yok");
                if (YesilSayisi > 0) p.Add($"{YesilSayisi} uygun");
                return string.Join(" · ", p);
            }
        }

        // Geriye dönük uyumluluk
        public int ToplamFark => ToplamOneriTest - ToplamTalepTest;
        public decimal FarkYuzde => ToplamTalepTest > 0
            ? ((decimal)(ToplamOneriTest - ToplamTalepTest) / ToplamTalepTest) * 100m
            : 0m;
    }

    public class DogrulamaFiltreVm
    {
        public string? Donem { get; set; }
        public SiparisTalepDurumu? Durum { get; set; } = SiparisTalepDurumu.Beklemede;
        public DogrulamaRengi? EnKotuRenk { get; set; }
        public int? HospitalID { get; set; }

        /// <summary>Onaylanan iki durumu (aynen + düzeltildi) birlikte göster.</summary>
        public bool Onaylananlar { get; set; }

        /// <summary>Durum süzgeci olmadan tüm talepler.</summary>
        public bool Tumu { get; set; }

        /// <summary>Gösterilecek sayfa (1'den başlar).</summary>
        public int Sayfa { get; set; } = 1;

        /// <summary>Sayfadaki talep sayısı: 25, 50 ya da 100.</summary>
        public int SayfaBoyu { get; set; } = 25;

        public List<SelectListItem> Donemler { get; set; } = new();
        public List<SelectListItem> Hastaneler { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    //  DETAY
    // ═══════════════════════════════════════════════════════════
    public class DogrulamaDetayVm
    {
        public int TalepID { get; set; }
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public string? HedefDonem { get; set; }
        public DateTime TalepTarihi { get; set; }
        public string? ExternalRefNo { get; set; }
        public string? HastaneNotu { get; set; }
        public SiparisTalepDurumu Durum { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }
        public string? OnayNotu { get; set; }

        public List<DogrulamaKalemVm> Kalemler { get; set; } = new();

        // ── Hastanenin veri durumu ──
        public DateTime? SonSayimTarihi { get; set; }
        public DateTime? KullanimSonAy { get; set; }
        public VeriGuveni Guven { get; set; }
        public List<string> HastaneUyarilari { get; set; } = new();
        public int HedefAy { get; set; } = 3;

        /// <summary>
        /// Siparişte olmayan ama stoku kritik/düşük ürünler —
        /// "hastane şunu istemeyi unutmuş olabilir".
        /// </summary>
        public List<StokHesapSatirVm> EksikKritikler { get; set; } = new();

        /// <summary>Bu hastanenin bekleyen başka talebi var mı?</summary>
        public int DigerBekleyenTalep { get; set; }

        public bool Bekliyor => Durum == SiparisTalepDurumu.Beklemede;

        // ── Özet ──
        public int ToplamTalepTest => Kalemler.Sum(k => k.HastaneTalepTest);
        public int ToplamOneriTest => Kalemler.Sum(k => k.OnerilenTest ?? 0);
        public int KirmiziSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Kirmizi);
        public int SariSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Sari);
        public int YesilSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Yesil);
        public int VeriYokSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.VeriYok);
        public int EslesmeyenSayisi => Kalemler.Count(k => !k.Eslesti);
        public bool TumuUygun => Kalemler.Count > 0 && YesilSayisi == Kalemler.Count;
    }

    public class DogrulamaKalemVm
    {
        public int KalemID { get; set; }

        // ── Ürün ──
        public string ExternalProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public int? TestID { get; set; }
        public string? TestAdi { get; set; }
        public UrunTipi UrunTipi { get; set; }
        public bool Eslesti => TestID.HasValue;

        // ── Birim ──
        /// <summary>1 kutu kaç test (adet birimli üründe 1).</summary>
        public int Carpan { get; set; } = 1;
        public bool KutuBirimli { get; set; }
        public string Birim => KutuBirimli ? "kutu" : "adet";

        // ── Hastane talebi ──
        public int HastaneTalepTest { get; set; }
        public decimal? HastaneTalepKutu { get; set; }

        /// <summary>İstenen miktar gösterim biriminde (kutu ya da adet).</summary>
        public decimal TalepMiktar => Carpan > 0 ? Math.Round((decimal)HastaneTalepTest / Carpan, 2) : HastaneTalepTest;

        // ── Stok ve tüketim ──
        public decimal? HesapGuncelStok { get; set; }

        /// <summary>Kayıtlar tutarsız (sayım + gönderim − kullanım eksi); stok gösterilmez.</summary>
        public bool KayitTutarsiz { get; set; }
        public decimal? OrtalamaAylikKullanim { get; set; }
        public decimal? StokAy { get; set; }
        public int Yolda { get; set; }

        /// <summary>Aynı ürün için bu talepten önce gelmiş bekleyen talepler (test).</summary>
        public int OncekiBekleyenTest { get; set; }

        public StokDurumu? StokDurumu { get; set; }
        public StokKaynagi StokKaynagi { get; set; }
        public VeriGuveni Guven { get; set; }
        public string? GuvenNedeni { get; set; }
        public decimal? TrendYuzde { get; set; }

        // ── Öneri ve hüküm ──
        public int? OnerilenTest { get; set; }
        public decimal? OnerilenKutu { get; set; }

        /// <summary>Önerilen miktar gösterim biriminde.</summary>
        public decimal OneriMiktar => OnerilenKutu ?? 0m;

        public decimal? SapmaYuzde { get; set; }
        public DogrulamaRengi DogrulamaRengi { get; set; }

        /// <summary>Kısa hüküm: "Uygun", "Fazla", "Az", "Gerek yok"…</summary>
        public string KararBaslik { get; set; } = "";

        /// <summary>Tek cümle gerekçe.</summary>
        public string? Gerekce { get; set; }

        /// <summary>Hesap özeti — "Sayım 800 + gönderim 500 − … = 866 test".</summary>
        public string? HesaplamaAciklamasi { get; set; }

        public List<HesapAdimVm> Adimlar { get; set; } = new();

        public decimal? SiparisSonrasiAy { get; set; }

        // ── Onay — düzenlenebilir ──
        public int? OnaylananTest { get; set; }

        /// <summary>Onaylanan miktar gösterim biriminde.</summary>
        public decimal OnaylananMiktar => Carpan > 0
            ? Math.Round((decimal)(OnaylananTest ?? HastaneTalepTest) / Carpan, 2)
            : (OnaylananTest ?? HastaneTalepTest);

        public string? KalemNotu { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  ONAY POST DTO'ları
    // ═══════════════════════════════════════════════════════════
    public class OnayPostDto
    {
        public int TalepID { get; set; }
        public string? OnayNotu { get; set; }
        public List<KalemOnayDto> Kalemler { get; set; } = new();
    }

    public class KalemOnayDto
    {
        public int KalemID { get; set; }

        /// <summary>
        /// Ekranda girilen miktar — kutu birimli üründe KUTU, diğerinde adet.
        /// Sunucu kalemin çarpanıyla teste çevirir.
        ///
        /// Bilerek METİN: tarayıcı "2.5" gönderir, tr-TR kültüründe ondalık
        /// bağlayıcı bunu 25 okur. Servis hem nokta hem virgülü kabul ederek
        /// kendisi ayrıştırır.
        /// </summary>
        public string? OnaylananMiktar { get; set; }

        /// <summary>Doğrudan test cinsinden (eski biçim; OnaylananMiktar boşsa kullanılır).</summary>
        public int? OnaylananTest { get; set; }

        public string? KalemNotu { get; set; }
    }

    public class RedPostDto
    {
        public int TalepID { get; set; }
        public string? RedNedeni { get; set; }
    }
}
