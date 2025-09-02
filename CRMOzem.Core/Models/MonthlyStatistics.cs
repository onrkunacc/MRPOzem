using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class MonthlyStatistics
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [ForeignKey(nameof(HospitalID))]
        public Hospital Hospital { get; set; }

        [Required]
        public int TestID { get; set; }

        [ForeignKey(nameof(TestID))]
        public Test Test { get; set; }

        [Required]
        public DateTime Month { get; set; }

        [Range(0, int.MaxValue)]
        public int TestCount { get; set; }
    }
}
