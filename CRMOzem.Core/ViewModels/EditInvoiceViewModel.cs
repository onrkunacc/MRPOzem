using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class EditInvoiceViewModel
    {
        public int FaturaId { get; set; }
        public int CariId { get; set; }
        public string CariAdi { get; set; }
        public int Ay { get; set; }
        public int Yil { get; set; }
        [Required]
        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = false)]
        public decimal Tutar { get; set; }
        public string? Ekler { get; set; }

    }
}
