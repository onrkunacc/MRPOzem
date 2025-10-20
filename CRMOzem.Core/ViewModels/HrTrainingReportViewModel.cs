using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HrTrainingReportViewModel
    {
        public int? SelectedTrainingId { get; set; }
        public List<HrTrainingItemViewModel> AvailableTrainings { get; set; } = new();
        public List<HrTrainingParticipationViewModel> Participations { get; set; } = new();
    }
}
