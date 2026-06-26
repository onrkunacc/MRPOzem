using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMOzem.Core.Models
{
    public class HospitalAylikIstatistik
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [ForeignKey(nameof(HospitalID))]
        public Hospital Hospital { get; set; } = null!;

        [Required]
        public int Yil { get; set; }      // 2026

        [Required, Range(1, 12)]
        public int Ay { get; set; }       // 1-12

        public bool GeldiMi { get; set; }

        [StringLength(2000)]
        public string? Not { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}