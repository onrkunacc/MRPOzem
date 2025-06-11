using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class FileItemViewModel
    {
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public string RelativePath { get; set; }
    }
}
