using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class FaturaViewModel
    {
        public string AyAdi { get; set; }
        public int AyIndex { get; set; }
        public decimal? Tutar { get; set; }
        public bool FaturaVarMi { get; set; }
        public int? FaturaId { get; set; } // Düzenleme için
        public int CariId { get; set; }
    }
}
