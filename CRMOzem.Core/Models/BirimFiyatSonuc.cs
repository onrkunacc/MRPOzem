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
    [Table("BirimFiyatSonuclari", Schema = "ozem")]
    public class BirimFiyatSonuc
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Karşılaştırma")]
        public int KarsilastirmaId { get; set; }

        [ForeignKey(nameof(KarsilastirmaId))]
        public virtual BirimFiyatKarsilastirma? Karsilastirma { get; set; }

        // ── Kimlik ───────────────────────────────────────────────
        [MaxLength(100)]
        [Display(Name = "Referans No")]
        public string? ReferansNo { get; set; }

        [MaxLength(100)]
        [Display(Name = "Referans (Normalize)")]
        public string ReferansNormalize { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Ürün Adı")]
        public string? UrunAdi { get; set; }

        [Column(TypeName = "date")]
        [Display(Name = "Dönem")]
        public DateTime? Donem { get; set; }

        // ── Kaynak satırlar ──────────────────────────────────────
        [Display(Name = "Tedarikçi Satırı")]
        public int? TedarikciSatirId { get; set; }

        [Display(Name = "Bizim Satır")]
        public int? BizSatirId { get; set; }

        [Display(Name = "Tedarikçi Fatura Tarihi")]
        public DateTime? TedarikciTarih { get; set; }

        [Display(Name = "Bizim Fatura Tarihi")]
        public DateTime? BizTarih { get; set; }

        /// <summary>Aynı ürün/dönem için birden fazla satır varsa kaç satırın ortalaması alındığı.</summary>
        [Display(Name = "Tedarikçi Satır Adedi")]
        public int TedarikciSatirAdedi { get; set; }

        [Display(Name = "Bizim Satır Adedi")]
        public int BizSatirAdedi { get; set; }

        // ── Fiyatlar ─────────────────────────────────────────────
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Tedarikçi Fiyat (USD)")]
        public decimal? TedarikciFiyatUsd { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Tedarikçi Fiyat (TL)")]
        public decimal? TedarikciFiyatTl { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Bizim Fiyat (TL)")]
        public decimal? BizFiyatTl { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Bizim Fiyat (USD)")]
        public decimal? BizFiyatUsd { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        [Display(Name = "Kullanılan Kur")]
        public decimal? Kur { get; set; }

        [Column(TypeName = "date")]
        [Display(Name = "Kur Tarihi")]
        public DateTime? KurTarihi { get; set; }

        // ── Fark ─────────────────────────────────────────────────
        /// <summary>Bizim fiyat − tedarikçi fiyatı, TL cinsinden. Pozitif = biz yüksek.</summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Fark (TL)")]
        public decimal? FarkTl { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Fark (USD)")]
        public decimal? FarkUsd { get; set; }

        /// <summary>Tedarikçi fiyatına göre yüzde sapma.</summary>
        [Column(TypeName = "decimal(12,4)")]
        [Display(Name = "Fark (%)")]
        public decimal? FarkYuzde { get; set; }

        /// <summary>Miktar bilindiğinde farkın toplam tutar etkisi.</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Tutar Etkisi (TL)")]
        public decimal? TutarEtkisiTl { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Miktar")]
        public decimal? Miktar { get; set; }

        [Display(Name = "Durum")]
        public SonucDurumu Durum { get; set; }

        [Display(Name = "Fark Yönü")]
        public FarkYonu Yon { get; set; } = FarkYonu.Esit;

        /// <summary>Tolerans dışında kalan, yani gerçekten incelenmesi gereken satır.</summary>
        [Display(Name = "Dikkat Gerektiriyor")]
        public bool DikkatGerektiriyor { get; set; }

        [MaxLength(500)]
        [Display(Name = "Not")]
        public string? Not { get; set; }

        // ── Hesaplanan (DB'ye yazılmaz) ──────────────────────────
        [NotMapped]
        public decimal MutlakFarkYuzde => FarkYuzde.HasValue ? Math.Abs(FarkYuzde.Value) : 0m;

        [NotMapped]
        public bool EslesmisMi => Durum == SonucDurumu.Eslesti;
    }
}
