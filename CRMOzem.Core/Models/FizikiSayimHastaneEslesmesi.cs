using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    [Table("FizikiSayimHastaneEslesmesi", Schema = "ozem")]
    public class FizikiSayimHastaneEslesmesi
    {
        public int ID { get; set; }

        [Required, MaxLength(300)]
        public string ApiHospitalName { get; set; } = "";

        public int? InternalHospitalID { get; set; }
        public Hospital? InternalHospital { get; set; }

        public bool Yoksay { get; set; } = false;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellemeTarihi { get; set; }

        [MaxLength(100)]
        public string? OlusturanKullanici { get; set; }
    }
}
