using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HrTrainingListViewModel
    {
        public List<HrTrainingItemViewModel> Trainings { get; set; } = new();

    }
}
