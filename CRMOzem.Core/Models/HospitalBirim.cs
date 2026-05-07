using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMOzem.Core.Models
{
    public class HospitalBirim
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [ForeignKey(nameof(HospitalID))]
        public Hospital Hospital { get; set; }

        [Required]
        public int BirimId { get; set; }

        [ForeignKey(nameof(BirimId))]
        public Birim Birim { get; set; }

        public bool Aktif { get; set; } = true;

        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }

        [MaxLength(50)]
        public string? IhaleKayitNo { get; set; }

        [MaxLength(500)]
        public string? Aciklama { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellenmeTarihi { get; set; }
    }
}