using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    [Table("HospitalEslesmesi", Schema = "ozem")]
    public class HospitalEslesmesi
    {
        [Key]
        public int ID { get; set; }

        /// <summary>PHP web_users.musteri_kodu (TS01, 96835 vb.)</summary>
        [Required, StringLength(50)]
        public string ExternalMusteriKodu { get; set; } = "";

        /// <summary>Admin eşleştirmesi için — PHP'deki görünen ad (snapshot amaçlı)</summary>
        [StringLength(200)]
        public string? ExternalKurumAdi { get; set; }

        /// <summary>Null ise bu musteri_kodu "yoksay" demek (dahili kullanıcılar)</summary>
        public int? InternalHospitalID { get; set; }

        [ForeignKey(nameof(InternalHospitalID))]
        public Hospital? InternalHospital { get; set; }

        /// <summary>Yoksay işareti — dahili kullanıcıları (ÖZ-EM vb.) sync'e dahil etmemek için</summary>
        public bool Yoksay { get; set; } = false;

        [StringLength(500)]
        public string? Not { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellemeTarihi { get; set; }

        [StringLength(100)]
        public string? OlusturanKullanici { get; set; }
    }
}
