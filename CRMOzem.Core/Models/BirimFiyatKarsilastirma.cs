using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
   
    [Table("BirimFiyatKarsilastirmalari", Schema = "ozem")]
    public class BirimFiyatKarsilastirma
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Karşılaştırma adı zorunludur.")]
        [MaxLength(250)]
        [Display(Name = "Karşılaştırma Adı")]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(2000)]
        [Display(Name = "Açıklama")]
        public string? Aciklama { get; set; }

        // ── Konum ────────────────────────────────────────────────
        [Display(Name = "Klasör")]
        public int? KlasorId { get; set; }

        [ForeignKey(nameof(KlasorId))]
        public virtual BirimFiyatKlasor? Klasor { get; set; }



        [Display(Name = "İhale")]
        public int? IhaleId { get; set; }

        [MaxLength(300)]
        [Display(Name = "İhale Adı")]
        public string? IhaleAdi { get; set; }

        [Display(Name = "Hastane")]
        public int? HospitalID { get; set; }

        [MaxLength(300)]
        [Display(Name = "Hastane Adı")]
        public string? HastaneAdi { get; set; }

        [MaxLength(100)]
        [Display(Name = "Tedarikçi Tarafı")]
        public string TedarikciEtiket { get; set; } = "Abbott";

        [MaxLength(100)]
        [Display(Name = "Bizim Taraf")]
        public string BizEtiket { get; set; } = "ÖZEM";

        [MaxLength(3)]
        [Display(Name = "Tedarikçi Para Birimi")]
        public string TedarikciParaBirimi { get; set; } = "USD";

        [MaxLength(3)]
        [Display(Name = "Bizim Para Birimi")]
        public string BizParaBirimi { get; set; } = "TRY";

        [Display(Name = "Eşleştirme Kuralı")]
        public EslestirmeKurali EslestirmeKurali { get; set; } = EslestirmeKurali.ReferansVeAy;

        [Display(Name = "Kur Kaynağı")]
        public KurKaynagi KurKaynagi { get; set; } = KurKaynagi.TcmbFaturaTarihi;

    
        [Column(TypeName = "decimal(18,6)")]
        [Display(Name = "Sabit Kur")]
        public decimal? SabitKur { get; set; }

       
        [Column(TypeName = "decimal(9,4)")]
        [Display(Name = "Tolerans (%)")]
        public decimal ToleransYuzde { get; set; } = 0m;

        [Display(Name = "Durum")]
        public KarsilastirmaDurumu Durum { get; set; } = KarsilastirmaDurumu.Taslak;

        [MaxLength(400)]
        [Display(Name = "Tedarikçi Dosyası")]
        public string? TedarikciDosyaAdi { get; set; }

        [Display(Name = "Tedarikçi Yükleme Tarihi")]
        public DateTime? TedarikciYuklemeTarihi { get; set; }

        [Display(Name = "Tedarikçi Satır Sayısı")]
        public int TedarikciSatirSayisi { get; set; }

        [MaxLength(400)]
        [Display(Name = "Bizim Dosya")]
        public string? BizDosyaAdi { get; set; }

        [Display(Name = "Bizim Yükleme Tarihi")]
        public DateTime? BizYuklemeTarihi { get; set; }

        [Display(Name = "Bizim Satır Sayısı")]
        public int BizSatirSayisi { get; set; }

       
        [Display(Name = "Eşleşen Satır")]
        public int ToplamEslesen { get; set; }

        [Display(Name = "Sadece Tedarikçide")]
        public int ToplamSadeceTedarikci { get; set; }

        [Display(Name = "Sadece Bizde")]
        public int ToplamSadeceBiz { get; set; }

        [Display(Name = "Farklı Ürün Sayısı")]
        public int FarkliUrunSayisi { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Toplam Fark (TL)")]
        public decimal ToplamFarkTl { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Toplam Fark (USD)")]
        public decimal ToplamFarkUsd { get; set; }

      
        [Column(TypeName = "decimal(9,4)")]
        [Display(Name = "Ortalama Sapma (%)")]
        public decimal OrtalamaSapmaYuzde { get; set; }

        [Display(Name = "Son Hesaplama")]
        public DateTime? SonHesaplamaTarihi { get; set; }

        
        [Display(Name = "Kuru Bulunamayan")]
        public int KuruBulunamayanSatir { get; set; }

        
        [Display(Name = "Oluşturma Tarihi")]
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        [MaxLength(200)]
        [Display(Name = "Oluşturan")]
        public string? OlusturanKullanici { get; set; }

        [Display(Name = "Güncelleme Tarihi")]
        public DateTime? GuncellemeTarihi { get; set; }

       
        public virtual ICollection<BirimFiyatSatir> Satirlar { get; set; } = new List<BirimFiyatSatir>();

        public virtual ICollection<BirimFiyatSonuc> Sonuclar { get; set; } = new List<BirimFiyatSonuc>();

       
        [NotMapped]
        public bool HerIkiDosyaYuklendi => TedarikciSatirSayisi > 0 && BizSatirSayisi > 0;

        [NotMapped]
        public int ToplamSatir => TedarikciSatirSayisi + BizSatirSayisi;

        [NotMapped]
        public decimal EslesmeOrani
        {
            get
            {
                var toplam = ToplamEslesen + ToplamSadeceTedarikci + ToplamSadeceBiz;
                return toplam == 0 ? 0m : Math.Round(ToplamEslesen * 100m / toplam, 1);
            }
        }
    }
}
