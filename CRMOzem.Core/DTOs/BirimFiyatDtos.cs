using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs
{
    public class ExcelOnizlemeDto
    {
        public bool Basarili { get; set; }
        public string? Hata { get; set; }

        public string DosyaAdi { get; set; } = string.Empty;

        public List<string> SayfaAdlari { get; set; } = new();
        public string SecilenSayfa { get; set; } = string.Empty;

        /// <summary>Başlık satırındaki kolon adları (boş olanlar "Kolon 3" gibi adlandırılır).</summary>
        public List<string> Basliklar { get; set; } = new();

        /// <summary>İlk N veri satırı — kullanıcı eşleştirmeyi gözüyle doğrulasın diye.</summary>
        public List<List<string>> OrnekSatirlar { get; set; } = new();

        public int ToplamVeriSatiri { get; set; }

        /// <summary>Başlık adlarından otomatik çıkarılan eşleşme önerisi.</summary>
        public KolonEslesmeDto Tahmin { get; set; } = new();
    }

    /// <summary>
    /// Excel kolonlarının hangi alana karşılık geldiği. İndeksler 0 tabanlıdır,
    /// -1 "seçilmedi" anlamına gelir.
    /// </summary>
    public class KolonEslesmeDto
    {
        public string? SayfaAdi { get; set; }

        /// <summary>Başlık satırının numarası (1 tabanlı).</summary>
        public int BaslikSatiri { get; set; } = 1;

        public int ReferansKolon { get; set; } = -1;
        public int UrunAdiKolon { get; set; } = -1;
        public int BirimFiyatKolon { get; set; } = -1;
        public int TarihKolon { get; set; } = -1;

        public int MiktarKolon { get; set; } = -1;
        public int FaturaNoKolon { get; set; } = -1;
        public int KurKolon { get; set; } = -1;
        public int BirimKolon { get; set; } = -1;

        /// <summary>Zorunlu alanların hepsi seçilmiş mi?</summary>
        public bool Gecerli =>
            ReferansKolon >= 0 && BirimFiyatKolon >= 0;
    }

    /// <summary>Excel okuma sonucu — kaç satır alındı, neler atlandı.</summary>
    public class ExcelOkumaSonucu
    {
        public bool Basarili { get; set; }
        public string? Hata { get; set; }

        public int OkunanSatir { get; set; }
        public int AtlananSatir { get; set; }

        /// <summary>Satır bazlı uyarılar — en fazla ilk 50 tanesi tutulur.</summary>
        public List<string> Uyarilar { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════════
    //  KUR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirli bir tarih için çözülmüş döviz kuru.</summary>
    public class KurSonucu
    {
        public bool Bulundu { get; set; }

        /// <summary>USD/TRY döviz satış kuru.</summary>
        public decimal Kur { get; set; }

        /// <summary>Kurun gerçekte alındığı tarih. Hafta sonu/tatilde önceki iş günü olur.</summary>
        public DateTime KurTarihi { get; set; }

        /// <summary>İstenen tarih — KurTarihi'nden farklıysa geriye gidilmiştir.</summary>
        public DateTime IstenenTarih { get; set; }

        public string? Kaynak { get; set; }
        public string? Hata { get; set; }

        public bool GeriyeGidildi => Bulundu && KurTarihi.Date != IstenenTarih.Date;
    }

    // ═══════════════════════════════════════════════════════════════
    //  HESAPLAMA
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Karşılaştırma hesaplamasının özeti.</summary>
    public class HesaplamaSonucu
    {
        public bool Basarili { get; set; }
        public string? Hata { get; set; }

        public int Eslesen { get; set; }
        public int SadeceTedarikci { get; set; }
        public int SadeceBiz { get; set; }
        public int DikkatGerektiren { get; set; }
        public int KuruBulunamayan { get; set; }

        public decimal ToplamFarkTl { get; set; }
        public decimal ToplamFarkUsd { get; set; }
        public decimal OrtalamaSapmaYuzde { get; set; }

        public List<string> Uyarilar { get; set; } = new();

        public string Ozet =>
            $"Eşleşen: {Eslesen} · Sadece tedarikçide: {SadeceTedarikci} · " +
            $"Sadece bizde: {SadeceBiz} · Dikkat: {DikkatGerektiren}";
    }
}
