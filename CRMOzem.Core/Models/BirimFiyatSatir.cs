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
    [Table("BirimFiyatSatirlari", Schema = "ozem")]
    public class BirimFiyatSatir
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Karşılaştırma")]
        public int KarsilastirmaId { get; set; }

        [ForeignKey(nameof(KarsilastirmaId))]
        public virtual BirimFiyatKarsilastirma? Karsilastirma { get; set; }

        [Display(Name = "Taraf")]
        public KarsilastirmaTarafi Taraf { get; set; }

        // ── Ürün ─────────────────────────────────────────────────
        [MaxLength(100)]
        [Display(Name = "Referans No")]
        public string? ReferansNo { get; set; }

        /// <summary>
        /// Eşleştirmede kullanılan normalize edilmiş referans
        /// (büyük harf, baştaki sıfırlar ve ayraçlar atılmış). Indeksli.
        /// </summary>
        [MaxLength(100)]
        [Display(Name = "Referans (Normalize)")]
        public string ReferansNormalize { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Ürün Adı")]
        public string? UrunAdi { get; set; }

        // ── Tarih ────────────────────────────────────────────────
        [Display(Name = "Fatura Tarihi")]
        public DateTime? FaturaTarihi { get; set; }

        /// <summary>Ay bazlı eşleştirme için ayın ilk günü. FaturaTarihi'nden türetilir.</summary>
        [Column(TypeName = "date")]
        [Display(Name = "Dönem")]
        public DateTime? Donem { get; set; }

        // ── Fiyat ────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Birim Fiyat")]
        public decimal BirimFiyat { get; set; }

        [MaxLength(3)]
        [Display(Name = "Para Birimi")]
        public string ParaBirimi { get; set; } = "TRY";

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Miktar")]
        public decimal? Miktar { get; set; }

        [MaxLength(50)]
        [Display(Name = "Birim")]
        public string? Birim { get; set; }

        // ── Kur dönüşümü (hesaplamada doldurulur) ────────────────
        [Column(TypeName = "decimal(18,6)")]
        [Display(Name = "Kur")]
        public decimal? Kur { get; set; }

        [Column(TypeName = "date")]
        [Display(Name = "Kur Tarihi")]
        public DateTime? KurTarihi { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Birim Fiyat (TL)")]
        public decimal? BirimFiyatTl { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Birim Fiyat (USD)")]
        public decimal? BirimFiyatUsd { get; set; }

        // ── Kaynak izi ───────────────────────────────────────────
        [Display(Name = "Excel Satır No")]
        public int KaynakSatirNo { get; set; }

        [MaxLength(300)]
        [Display(Name = "Fatura No")]
        public string? FaturaNo { get; set; }

        /// <summary>Okuma sırasında bu satırla ilgili uyarı (eksik tarih, hatalı fiyat vb.).</summary>
        [MaxLength(500)]
        [Display(Name = "Uyarı")]
        public string? Uyari { get; set; }

        [Display(Name = "Oluşturma Tarihi")]
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    }
}
