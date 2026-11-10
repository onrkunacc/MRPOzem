using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.Models
{
    public class PersonelGider
    {
        public int Id { get; set; }

        public int IhaleId { get; set; }
        public Ihale Ihale { get; set; }

        public int BirimId { get; set; }
        public Birim? Birim { get; set; }

        /// <summary>
        /// Şartnamedeki kısım numarası (1..Ihale.KisimSayisi).
        /// null = tüm kısımlara ortak personel; kısım toplamlarına
        /// dağıtılmaz, "Ortak Giderler" bloğunda raporlanır.
        /// </summary>
        [Display(Name = "Kısım")]
        public int? KisimNo { get; set; }

        [Required, MaxLength(200)]
        public string GorevUnvan { get; set; } = string.Empty;

        public int Sayi { get; set; } = 1;

        [MaxLength(100)]
        public string? EgitimDurumu { get; set; }

        [MaxLength(50)]
        public string UcretTipi { get; set; } = "Brut (Aylik)";

        [Column(TypeName = "decimal(18,2)")]
        public decimal BirimFiyatAylik { get; set; }

        // SureGun PARAMETRELER'den otomatik gelir (IhaleSuresiGun)
        // ToplamTRY = Sayi * BirimFiyatAylik * IhaleSuresiGun / 30

        [MaxLength(300)]
        public string? Aciklama { get; set; }

        [NotMapped]
        public decimal ToplamTRY => Sayi * BirimFiyatAylik * (Ihale?.IhaleSuresiGun ?? 720) / 30m;

        /// <summary>Ekranlarda gösterilecek kısım etiketi.</summary>
        [NotMapped]
        public string KisimEtiket => KisimNo.HasValue ? $"{KisimNo}. Kısım" : "Ortak";
    }
}
