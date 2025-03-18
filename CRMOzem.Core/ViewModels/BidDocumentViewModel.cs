using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class BidDocumentViewModel
    {
        public string CurrentFolder { get; set; }
        public List<string> Files { get; set; } = new();
        public List<string> SubFolders { get; set; } = new();
    }
}
