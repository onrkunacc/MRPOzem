using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Ihale
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string IhaleAdi { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IhaleKayitNo { get; set; }

        public DateTime? IhaleTarihi { get; set; }
        public TimeSpan? IhaleSaati { get; set; }

        [MaxLength(100)]
        public string? IhaleSekli { get; set; }

        public int IhaleSuresiGun { get; set; } = 720;

        /// <summary>
        /// Şartnamenin kaç kısımdan oluştuğu. Kalem ve gider satırları
        /// 1..KisimSayisi aralığında bir kısma atanır; atanmayanlar
        /// "ortak gider" olarak raporlanır.
        /// </summary>
        [Range(1, 99, ErrorMessage = "Kısım sayısı 1 ile 99 arasında olmalıdır.")]
        [Display(Name = "Şartname Kısım Sayısı")]
        public int KisimSayisi { get; set; } = 1;

        [Column(TypeName = "decimal(5,4)")]
        public decimal GenelGiderOran { get; set; } = 0.10m;

        [Column(TypeName = "decimal(5,4)")]
        public decimal KarOran { get; set; } = 0.15m;

        [Column(TypeName = "decimal(5,4)")]
        public decimal KDVOran { get; set; } = 0.20m;

        [Column(TypeName = "decimal(10,4)")]
        public decimal KurUSD { get; set; } = 50m;

        [Column(TypeName = "decimal(10,4)")]
        public decimal KurEUR { get; set; } = 60m;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellenmeTarihi { get; set; }

        public bool Aktif { get; set; } = true;

        // Navigation
        public ICollection<PersonelGider> PersonelGiderler { get; set; } = new List<PersonelGider>();
        public ICollection<EkGider> EkGiderler { get; set; } = new List<EkGider>();
        public ICollection<IhaleKalem> IhaleKalemler { get; set; } = new List<IhaleKalem>();

        // ── Hesaplananlar (DB'ye yazılmaz) ──────────────────────────────

        /// <summary>1..KisimSayisi — form ve tablolardaki kısım seçimlerini doldurur.</summary>
        [NotMapped]
        public IEnumerable<int> Kisimlar => Enumerable.Range(1, Math.Max(1, KisimSayisi));

        /// <summary>Birden fazla kısım varsa ekranlarda kısım kırılımı gösterilir.</summary>
        [NotMapped]
        public bool CokKisimli => KisimSayisi > 1;
    }
}
