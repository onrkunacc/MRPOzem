using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class IhaleKalem
    {
        public int Id { get; set; }

        public int IhaleId { get; set; }
        public Ihale Ihale { get; set; }

        public int? BirimId { get; set; }
        public Birim? Birim { get; set; }

 
        [Display(Name = "Kısım")]
        public int? KisimNo { get; set; }

        public int SiraNo { get; set; }

        [MaxLength(50)]
        public string? SutKodu { get; set; }

        [Required, MaxLength(300)]
        public string TetkikAdi { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TestMiktari { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal SutFiyat { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal SutCarpan { get; set; } = 0.593m;

        // Hesaplananlar
        // ToplamSUT = TestMiktari * SutFiyat
        // ToplamSutFiyat = ToplamSUT * SutCarpan
        [NotMapped]
        public decimal ToplamSUT => TestMiktari * SutFiyat;

        [NotMapped]
        public decimal ToplamSutFiyat => ToplamSUT * SutCarpan;

        /// <summary>Ekranlarda gösterilecek kısım etiketi.</summary>
        [NotMapped]
        public string KisimEtiket => KisimNo.HasValue ? $"{KisimNo}. Kısım" : "Ortak";
    }
}
