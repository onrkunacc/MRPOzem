using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.Models
{
    public class Hospital
    {
        [Key]
        public int HospitalID { get; set; }

        [Required]
        [StringLength(200)]
        public string HospitalName { get; set; } = null!;

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        // ── İhale takip alanları ──
        public DateTime? IhaleTarihi { get; set; }

        [StringLength(100)]
        public string? IhaleNumarasi { get; set; }

        public bool Aktif { get; set; } = true;

        // ── Navigations ──
        public ICollection<TestTemplate> TestTemplates { get; set; }
        public ICollection<MonthlyStatistics> Statistics { get; set; }
        public ICollection<MonthlyShipment> Shipments { get; set; }
        public ICollection<HospitalTestMonthlyVolume> MonthlyVolumes { get; set; } = new List<HospitalTestMonthlyVolume>();
        public ICollection<HospitalBirim> HospitalBirimler { get; set; } = new List<HospitalBirim>();

        // YENİ
        public ICollection<HospitalAylikIstatistik> AylikIstatistikler { get; set; } = new List<HospitalAylikIstatistik>();
    }
}