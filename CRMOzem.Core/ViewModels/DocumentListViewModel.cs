using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class DocumentListViewModel
    {
        public List<FollowDocument> Documents { get; set; }
        public string SearchTerm { get; set; }
        public int Page {  get; set; }
        public int TotalPages { get; set; }
    }
}
