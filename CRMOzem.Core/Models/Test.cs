using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.Models
{
    public class Test
    {
        [Key]
        public int TestID { get; set; }

        [Required]
        [StringLength(100)]
        public string TestName { get; set; }

        [StringLength(30)]
        public string? SutCode { get; set; }
        [StringLength(50)]
        public string? Kategori { get; set; }  // "Alinity Sarf", "Architect CC" vb

        [StringLength(50)]
        public string? Platform { get; set; }  // "Alinity C", "Architect IA" vb

        // ── YENİ: Giriş tipi ve kutu bilgisi ──
        public TestGirisTipi GirisTipi { get; set; } = TestGirisTipi.Adet;

        /// <summary>
        /// Sadece GirisTipi=Kutu için anlamlı.
        /// Örn: Glukoz testi → 100 adet/kutu.
        /// </summary>
        public int? KutuBasinaAdet { get; set; }

        // İlişkiler
        public ICollection<TestTemplate> Templates { get; set; }
        public ICollection<MonthlyStatistics> Statistics { get; set; }
        public ICollection<MonthlyShipment> Shipments { get; set; }
        public ICollection<TestSutPoint> SutPoints { get; set; } = new List<TestSutPoint>();
        public ICollection<HospitalTestMonthlyVolume> MonthlyVolumes { get; set; } = new List<HospitalTestMonthlyVolume>();
    }
}