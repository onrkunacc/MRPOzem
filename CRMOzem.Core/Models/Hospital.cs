using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Hospital
    {
        [Key]
        public int HospitalID { get; set; }

        [Required]
        [StringLength(200)]
        public string HospitalName { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        // Navigation
        public ICollection<TestTemplate> TestTemplates { get; set; }
        public ICollection<MonthlyStatistics> Statistics { get; set; }
        public ICollection<MonthlyShipment> Shipments { get; set; }
    }
}
