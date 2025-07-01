using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class CreateLeaveRequestViewModel
    {
        public int LeaveTypeId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public TimeSpan? StartHour { get; set; }
        public TimeSpan? EndHour { get; set; }

        public string? Reason { get; set; }

        public List<LeaveType> LeaveTypes { get; set; } // dropdown için
    }

}
