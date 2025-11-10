using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class UserTrainingListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime TrainingDate { get; set; }
        public bool IsJoined { get; set; }
        public bool IsCompleted { get; set; }

        public string Link { get; set; } = string.Empty; 

    }
}
