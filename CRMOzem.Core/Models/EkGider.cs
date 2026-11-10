using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class EkGider
    {
        public int Id { get; set; }

        public int IhaleId { get; set; }
        public Ihale Ihale { get; set; }

        public int? BirimId { get; set; }
        public Birim? Birim { get; set; }

        /// <summary>
        /// Şartnamedeki kısım numarası (1..Ihale.KisimSayisi).
        /// null = tüm kısımlara ortak gider; kısım toplamlarına
        /// dağıtılmaz, "Ortak Giderler" bloğunda raporlanır.
        /// </summary>
        [Display(Name = "Kısım")]
        public int? KisimNo { get; set; }

        [Required, MaxLength(200)]
        public string GiderAdi { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Kategori { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Miktar { get; set; }

        [MaxLength(30)]
        public string? BirimAciklama { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal BirimFiyat { get; set; }

        [MaxLength(10)]
        public string ParaBirimi { get; set; } = "TRY";

        [Column(TypeName = "decimal(18,4)")]
        public decimal Kur { get; set; } = 1m;

        [MaxLength(300)]
        public string? Aciklama { get; set; }

        // TutarTRY = Miktar * BirimFiyat * Kur
        [NotMapped]
        public decimal TutarTRY => Miktar * BirimFiyat * Kur;

        /// <summary>Ekranlarda gösterilecek kısım etiketi.</summary>
        [NotMapped]
        public string KisimEtiket => KisimNo.HasValue ? $"{KisimNo}. Kısım" : "Ortak";
    }
}
