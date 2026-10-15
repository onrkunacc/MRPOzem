using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.Models
{
  
    public class KotaHospital
    {
        public int KotaHospitalID { get; set; }

        public int KotaID { get; set; }
        public Kota? Kota { get; set; }

        public int HospitalID { get; set; }
        public Hospital? Hospital { get; set; }

        public DateTime EklenmeTarihi { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Aciklama { get; set; }
    }
}