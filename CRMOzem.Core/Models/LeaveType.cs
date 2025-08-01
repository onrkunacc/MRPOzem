using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class LeaveType
    {
        public int Id { get; set; }
        [Required, MaxLength(100)]
        public string Name { get; set; }
        public string? Description { get; set; }    
        public bool IsDeductedFromQuota  { get; set; } // Yıllık haktan düşer mi ? 
        public bool IsHourly {  get; set; } // Saatlik mi ? 
        public bool IsPaid { get; set; } // Ücretli mi ? 
        public ICollection<LeaveRequest> LeaveRequests { get; set; }

    }
}
