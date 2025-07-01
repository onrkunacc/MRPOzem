using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class LeaveStatus
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // Türkçe statü adı (örn: Onaylandı)

        [MaxLength(100)]
        public string? Code { get; set; } // sistemsel ad (örn: Approved)

        public string? Description { get; set; }

        public ICollection<LeaveRequest> LeaveRequests { get; set; }
    }
}
