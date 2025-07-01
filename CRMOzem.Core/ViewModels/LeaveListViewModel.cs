using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class LeaveListViewModel
    {
        public List<LeaveRequest> Leaves { get; set; }
        public int RemainingDays { get; set; }
    }

}
