using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TestTemplate
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

        public bool IsActive { get; set; } = true;
    }
}
