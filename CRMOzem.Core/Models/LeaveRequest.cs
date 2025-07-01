using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Enums;

namespace CRMOzem.Core.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required]
        public int LeaveTypeId { get; set; }
        public LeaveType LeaveType { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public TimeSpan? StartHour { get; set; } // saatlik izin için
        public TimeSpan? EndHour { get; set; }

        public string? Reason { get; set; }

        public int StatusId { get; set; }

        [ForeignKey("StatusId")]
        public LeaveStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
