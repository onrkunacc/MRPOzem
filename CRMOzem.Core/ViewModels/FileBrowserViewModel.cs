using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class FileBrowserViewModel
    {
        public string CurrentPath { get; set; }
        public List<FileItemViewModel> Items { get; set; }
    }
}
