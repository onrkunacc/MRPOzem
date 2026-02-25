using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class HospitalTestMonthlyVolume
    {
        [Key]
        public int HospitalTestMonthlyVolumeID { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [Required]
        public int TestID { get; set; }

        [Required]
        public int Year { get; set; }

        [Required, Range(1, 12)]
        public int Month { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int Count { get; set; } 

        public Hospital Hospital { get; set; }
        public Test Test { get; set; }
    }
}
