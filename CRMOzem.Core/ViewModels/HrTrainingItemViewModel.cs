using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HrTrainingItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime TrainingDate { get; set; }
        public bool IsPublished { get; set; }
        public string Link { get; set; } = string.Empty;

    }
}
