using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        public IEnumerable<SelectListItem> LeaveTypes { get; set; } = Enumerable.Empty<SelectListItem>();
    }

}
