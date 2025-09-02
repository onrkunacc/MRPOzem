using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Test
    {
        [Key]
        public int TestID { get; set; }

        [Required]
        [StringLength(100)]
        public string TestName { get; set; }

        // İlişkiler
        public ICollection<TestTemplate> Templates { get; set; }
        public ICollection<MonthlyStatistics> Statistics { get; set; }
        public ICollection<MonthlyShipment> Shipments { get; set; }
    }
}
