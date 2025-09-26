using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class UserTrainingListViewModel
    {
        public List<UserTrainingListItemViewModel> Items { get; set; } = new();
    }
}
