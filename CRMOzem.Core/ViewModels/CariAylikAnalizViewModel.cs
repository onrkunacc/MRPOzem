using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class CariAylikAnalizViewModel
    {
        public string CariAdi { get; set; }
        public List<decimal> AylikTutarlar { get; set; } = new();
    }
}
