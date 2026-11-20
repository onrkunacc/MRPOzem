using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMOzem.Core.ViewModels
{
    // ═══════════════════════════════════════════════════════════════
    //  HESAP KURALLARI — tek yerde
    // ═══════════════════════════════════════════════════════════════
    //  appsettings.json içinde "Lojistik" bölümü varsa oradan okunur,
    //  yoksa buradaki varsayılanlar geçerlidir. Bölüm eklemek zorunlu
    //  değil.
    //
    //    "Lojistik": { "HedefAy": 3, "TedarikGun": 21 }
    // ═══════════════════════════════════════════════════════════════
    public class LojistikAyar
    {
        /// <summary>Hastanede tutulmak istenen stok — ay cinsinden.</summary>
        public int HedefAy { get; set; } = 3;

        /// <summary>Abbott'tan dışarıdan çekimde tedarik süresi (gün).</summary>
        public int TedarikGun { get; set; } = 21;

        /// <summary>Aylık tüketim ortalaması kaç raporlanmış aydan alınır.</summary>
        public int OrtalamaAy { get; set; } = 3;

        /// <summary>
        /// Stok hedefin bu kadar (ay) altına inmeden sipariş önerilmez.
        /// Her küçük eksikte "1 kutu daha" önerisi çıkmasın diye.
        /// </summary>
        public decimal OneriEsigiAy { get; set; } = 0.5m;

        /// <summary>
        /// Fiziki sayımı olmayan üründe stok, bugüne kadarki gönderim − kullanım
        /// farkından tahmin edilsin mi? VARSAYILAN: HAYIR.
        /// Gönderim kayıtları eksikse ya da gönderim ve kullanım farklı birimle
        /// (kutu / test) girildiyse bu fark anlamsız, çoğu zaman eksi çıkar.
        /// Sayımı olmayan ürün "Sayım yok" görünür; açmak için
        /// appsettings → "Lojistik": { "SayimsizTahmin": true }.
        /// </summary>
        public bool SayimsizTahmin { get; set; }

        /// <summary>Sayım bu günden eskiyse güven "orta"ya iner.</summary>
        public int SayimEskiGun { get; set; } = 120;

        /// <summary>Sayım bu günden eskiyse güven "düşük"e iner.</summary>
        public int SayimCokEskiGun { get; set; } = 180;

        /// <summary>Stok bu günden az yetecekse "kritik" — tedarik süresi + pay.</summary>
        public int KritikGun => TedarikGun + 9;
        public int DusukGun => 60;
        public int IzlemeGun => 90;
    }

    // ═══════════════════════════════════════════════════════════════
    //  HASTANE PANELİ
    // ═══════════════════════════════════════════════════════════════
    public class HastaneStokPaneliVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        // ── Sayım ──
        public DateTime? SonSayimTarihi { get; set; }
        public int SonSayimNo { get; set; }
        public bool SayimVar => SonSayimTarihi.HasValue;
        public int? GunlerdenSayimdanBeri => SonSayimTarihi.HasValue
            ? (int)(DateTime.Today - SonSayimTarihi.Value.Date).TotalDays
            : null;

        // ── Verinin güncelliği ──
        /// <summary>Hastanenin kullanım (istatistik) bildirdiği son ay.</summary>
        public DateTime? KullanimSonAy { get; set; }

        /// <summary>Gönderim kaydı girilmiş son ay.</summary>
        public DateTime? GonderimSonAy { get; set; }

        /// <summary>Son 12 ayda kullanım bildirilen ay sayısı.</summary>
        public int RaporlananAySayisi { get; set; }

        // ── Kurallar ──
        public int HedefRunwayAy { get; set; } = 3;
        public int TedarikGun { get; set; } = 21;

        /// <summary>Hastane düzeyinde uyarılar — ekranın üstünde gösterilir.</summary>
        public List<string> Uyarilar { get; set; } = new();

        public List<StokHesapSatirVm> Satirlar { get; set; } = new();

        // ── Özet (satırlardan türetilir) ──
        public int ToplamUrun => Satirlar.Count;
        public int KritikSayisi => Satirlar.Count(s => s.Durum == StokDurumu.Kritik);
        public int DusukSayisi => Satirlar.Count(s => s.Durum == StokDurumu.Dusuk);
        public int IzlemedeSayisi => Satirlar.Count(s => s.Durum == StokDurumu.Izlemede);
        public int SaglikliSayisi => Satirlar.Count(s => s.Durum == StokDurumu.Saglikli);
        public int DurgunSayisi => Satirlar.Count(s => s.Durum == StokDurumu.Durgun);
        public int VeriYokSayisi => Satirlar.Count(s => s.Durum == StokDurumu.VeriYok);

        public int OnerilenKalemSayisi => Satirlar.Count(s => s.OnerilenTest > 0);
        public int OnerilenToplamTest => Satirlar.Sum(s => s.OnerilenTest);
        public int OnerilenToplamKutu => Satirlar.Where(s => s.KutuBirimli).Sum(s => s.OnerilenKutu);

        /// <summary>Hastanenin genel veri güveni — satırların çoğunluğuna göre.</summary>
        public VeriGuveni Guven
        {
            get
            {
                var hesaplanan = Satirlar.Where(s => s.Guven != VeriGuveni.Yok).ToList();
                if (hesaplanan.Count == 0) return VeriGuveni.Yok;

                var yuksek = hesaplanan.Count(s => s.Guven == VeriGuveni.Yuksek);
                var dusuk = hesaplanan.Count(s => s.Guven == VeriGuveni.Dusuk);

                if (yuksek * 2 >= hesaplanan.Count) return VeriGuveni.Yuksek;
                if (dusuk * 2 > hesaplanan.Count) return VeriGuveni.Dusuk;
                return VeriGuveni.Orta;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  TEK ÜRÜN (hastane × test)
    // ═══════════════════════════════════════════════════════════════
    public class StokHesapSatirVm
    {
        // ── Ürün ──
        public int? TestID { get; set; }
        public string TestAdi { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public string ProductName { get; set; } = "";
        public string? Platform { get; set; }
        public string? Kategori { get; set; }
        public UrunTipi UrunTipi { get; set; }

        // ── Birim ──
        /// <summary>1 kutu kaç test. Adet birimli üründe 1.</summary>
        public int BoxTextCount { get; set; } = 1;
        public bool KutuBirimli { get; set; }
        public string Birim => KutuBirimli ? "kutu" : "adet";

        // ── Stok: nereden geldi ──
        public StokKaynagi StokKaynagi { get; set; }

        public DateTime? SonSayimTarihi { get; set; }
        public int SonSayimNo { get; set; }
        public int SonSayimTestCount { get; set; }
        public int LotSayisi { get; set; }

        /// <summary>Sayımdan (ya da kayıt başından) bu yana giren.</summary>
        public int SayimdanSonraGonderim { get; set; }

        /// <summary>Sayımdan (ya da kayıt başından) bu yana raporlanan kullanım.</summary>
        public int SayimdanSonraKullanim { get; set; }

        /// <summary>
        /// Kullanımı henüz raporlanmamış günler için tahmin edilen tüketim.
        /// Eski hesapta yoktu — stok bu yüzden olduğundan yüksek görünüyordu.
        /// </summary>
        public int TahminiTuketim { get; set; }
        public int TahminGun { get; set; }

        /// <summary>Bugün itibarıyla tahmini stok (test).</summary>
        public int HesapGuncelStok { get; set; }

        /// <summary>Onaylanmış ama gönderim kaydına henüz işlenmemiş miktar (test).</summary>
        public int Yolda { get; set; }

        /// <summary>Henüz karara bağlanmamış taleplerdeki miktar (test).</summary>
        public int BekleyenTalep { get; set; }

        // ── Tüketim ──
        public TuketimKaynagi TuketimKaynagi { get; set; }
        public int OrtalamaAySayisi { get; set; }
        public int Son3AyKullanim { get; set; }
        public decimal OrtalamaAylikKullanim { get; set; }

        /// <summary>Son dönem ortalamasının bir önceki döneme göre değişimi (%).</summary>
        public decimal? TrendYuzde { get; set; }

        // ── Yetme süresi ──
        public int RunwayGun { get; set; }

        /// <summary>Eldeki stok kaç ay yeter. Tüketim yoksa null.</summary>
        public decimal? StokAy { get; set; }

        // ── Öneri ──
        public int HedefStok { get; set; }

        /// <summary>Önerilen miktar — test cinsinden, kutuya yuvarlanmış.</summary>
        public int OnerilenTest { get; set; }

        /// <summary>Önerilen kutu (adet birimli üründe adet).</summary>
        public int OnerilenKutu { get; set; }

        // ── Sonuç ──
        public StokDurumu Durum { get; set; }
        public VeriGuveni Guven { get; set; }
        public string? GuvenNedeni { get; set; }

        /// <summary>Tek satırlık hesap özeti — "800 + 500 − 620 − 180 = 500 test".</summary>
        public string HesaplamaOzeti { get; set; } = "";

        /// <summary>"Nasıl hesaplandı" açılır penceresi için adım adım döküm.</summary>
        public List<HesapAdimVm> Adimlar { get; set; } = new();

        /// <summary>
        /// Tüketim eşdeğer testten alındıysa (platform geçişi) o testlerin adları,
        /// örn. "Anti CCP -100 (Architect Küçük Kutu)".
        /// </summary>
        public string? EsdegerTestler { get; set; }

        /// <summary>
        /// Ürün hastanenin son sayımında yoktu; sayım tarihindeki stoğu 0 kabul
        /// edildi (yalnızca platform geçişindeki yeni ürünler için).
        /// </summary>
        public bool SayimdaYokSifir { get; set; }

        /// <summary>
        /// Hesaplanan stok eksi çıktı: sayımdan sonra kaydedilen kullanım,
        /// sayım + gönderimden fazla. Gerçek bir eksiklik değil, kayıt
        /// tutarsızlığıdır (çoğunlukla eksik gönderim kaydı). Bu durumda
        /// öneri ve "kaç ay yeter" hesaplanmaz.
        /// </summary>
        public bool KayitTutarsiz { get; set; }

        public List<string> Uyarilar { get; set; } = new();

        // ── Gösterim yardımcıları ──
        public decimal? StokKutu => KutuBirimli && BoxTextCount > 0
            ? Math.Round((decimal)HesapGuncelStok / BoxTextCount, 1)
            : null;

        public decimal? AylikKutu => KutuBirimli && BoxTextCount > 0
            ? Math.Round(OrtalamaAylikKullanim / BoxTextCount, 1)
            : null;

        public bool SiparisGerekli => OnerilenTest > 0;
    }

    public class HesapAdimVm
    {
        public string Etiket { get; set; } = "";
        public string Deger { get; set; } = "";
        public string? Not { get; set; }

        /// <summary>"+" / "−" / "=" / "" — satırın başındaki işaret.</summary>
        public string Isaret { get; set; } = "";

        public HesapAdimVm() { }
        public HesapAdimVm(string isaret, string etiket, string deger, string? not = null)
        {
            Isaret = isaret; Etiket = etiket; Deger = deger; Not = not;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  TÜM HASTANELER — özet satırı
    // ═══════════════════════════════════════════════════════════════
    public class HastaneStokOzetVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public DateTime? SonSayimTarihi { get; set; }
        public int? SayimGun { get; set; }
        public DateTime? KullanimSonAy { get; set; }
        public DateTime? GonderimSonAy { get; set; }
        public VeriGuveni Guven { get; set; }

        public int UrunSayisi { get; set; }
        public int KritikSayisi { get; set; }
        public int DusukSayisi { get; set; }
        public int DurgunSayisi { get; set; }
        public int OnerilenKalem { get; set; }
        public int OnerilenKutu { get; set; }
        public int BekleyenTalepSayisi { get; set; }

        public List<string> Uyarilar { get; set; } = new();

        /// <summary>Listede en üstte ne görünsün: kritik çok olan, sonra düşük.</summary>
        public int OncelikPuani => KritikSayisi * 100 + DusukSayisi * 10 + OnerilenKalem;
    }

    public class StokOzetSayfaVm
    {
        public List<HastaneStokOzetVm> Hastaneler { get; set; } = new();
        public int HedefAy { get; set; } = 3;
        public int TedarikGun { get; set; } = 21;

        public int HastaneSayisi => Hastaneler.Count;
        public int SayimiOlan => Hastaneler.Count(h => h.SonSayimTarihi.HasValue);
        public int ToplamUrun => Hastaneler.Sum(h => h.UrunSayisi);
        public int ToplamKritik => Hastaneler.Sum(h => h.KritikSayisi);
        public int ToplamDusuk => Hastaneler.Sum(h => h.DusukSayisi);
        public int ToplamDurgun => Hastaneler.Sum(h => h.DurgunSayisi);
        public int ToplamOnerilenKalem => Hastaneler.Sum(h => h.OnerilenKalem);
        public int ToplamOnerilenKutu => Hastaneler.Sum(h => h.OnerilenKutu);

        public int GuvenYuksek => Hastaneler.Count(h => h.Guven == VeriGuveni.Yuksek);
        public int GuvenOrta => Hastaneler.Count(h => h.Guven == VeriGuveni.Orta);
        public int GuvenDusuk => Hastaneler.Count(h => h.Guven == VeriGuveni.Dusuk);
        public int GuvenYok => Hastaneler.Count(h => h.Guven == VeriGuveni.Yok);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SİPARİŞ KARARI — bir talep kalemi için hüküm
    // ═══════════════════════════════════════════════════════════════
    public class SiparisKararVm
    {
        public DogrulamaRengi Renk { get; set; } = DogrulamaRengi.VeriYok;

        /// <summary>Kısa hüküm: "Uygun", "Fazla", "Az", "Gerek yok", "Veri yok"…</summary>
        public string Baslik { get; set; } = "Veri yok";

        /// <summary>Tek cümle gerekçe — kullanıcıya "neden" sorusunun cevabı.</summary>
        public string Gerekce { get; set; } = "";

        /// <summary>(Talep − Öneri) / Öneri × 100, kutu bazında. Öneri 0 ise null.</summary>
        public decimal? SapmaYuzde { get; set; }

        public int OnerilenTest { get; set; }
        public int OnerilenKutu { get; set; }

        /// <summary>Bu sipariş onaylanırsa stok kaç ay yeter.</summary>
        public decimal? SiparisSonrasiAy { get; set; }

        /// <summary>Kutu (ya da adet) farkı: talep − öneri.</summary>
        public decimal Fark { get; set; }
    }
}
