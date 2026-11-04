using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{

    [Table("BirimFiyatKlasorleri", Schema = "ozem")]
    public class BirimFiyatKlasor
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Klasör adı zorunludur.")]
        [MaxLength(200)]
        [Display(Name = "Klasör Adı")]
        public string Ad { get; set; } = string.Empty;


        [Display(Name = "Üst Klasör")]
        public int? UstKlasorId { get; set; }

        [ForeignKey(nameof(UstKlasorId))]
        public virtual BirimFiyatKlasor? UstKlasor { get; set; }

        public virtual ICollection<BirimFiyatKlasor> AltKlasorler { get; set; } = new List<BirimFiyatKlasor>();

        public virtual ICollection<BirimFiyatKarsilastirma> Karsilastirmalar { get; set; } = new List<BirimFiyatKarsilastirma>();

        [MaxLength(1000)]
        [Display(Name = "Açıklama")]
        public string? Aciklama { get; set; }

        [MaxLength(9)]
        [Display(Name = "Renk")]
        public string? Renk { get; set; }

      
        [Display(Name = "Sıra")]
        public int Sira { get; set; }

        [Display(Name = "Aktif")]
        public bool Aktif { get; set; } = true;

        [Display(Name = "Oluşturma Tarihi")]
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        [MaxLength(200)]
        [Display(Name = "Oluşturan")]
        public string? OlusturanKullanici { get; set; }

        [Display(Name = "Güncelleme Tarihi")]
        public DateTime? GuncellemeTarihi { get; set; }

 
        [NotMapped]
        public int KarsilastirmaAdedi { get; set; }

        [NotMapped]
        public int ToplamKarsilastirmaAdedi { get; set; }

       
        [NotMapped]
        public int Seviye { get; set; }
    }
}
