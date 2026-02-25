using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TestSutPoint
    {

        [Key]
        public int TestSutPointID { get; set; }

        [Required]
        public int TestID { get; set; }

        [Required]
        public int Year { get; set; } 

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal Point { get; set; } 

        public Test Test { get; set; }
    }
}
