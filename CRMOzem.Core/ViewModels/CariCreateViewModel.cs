using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Core.ViewModels
{
    public class CariCreateViewModel
    {
        public Cariler Cari { get; set; } = new Cariler();
        [BindNever]
        public IEnumerable<SelectListItem> CariGruplari { get; set; }
    }
}
