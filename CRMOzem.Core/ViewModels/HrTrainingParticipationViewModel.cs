using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HrTrainingParticipationViewModel
    {
        public string UserName { get; set; }
        public bool HasParticipated { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? JoinedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
