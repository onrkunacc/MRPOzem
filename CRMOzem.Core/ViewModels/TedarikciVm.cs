using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class TedarikciListVm
    {
        public int TedarikciID { get; set; }
        public string Ad { get; set; } = "";
        public TedarikciGrup Grup { get; set; }
        public string? Aciklama { get; set; }
        public bool Aktif { get; set; }
        public int Sira { get; set; }
        public int HareketSayisi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
    }

    public class TedarikciCreateVm
    {
        [Required(ErrorMessage = "Ad zorunlu")]
        [StringLength(150)]
        public string Ad { get; set; } = "";

        [Required]
        public TedarikciGrup Grup { get; set; } = TedarikciGrup.Diger;

        [StringLength(500)]
        public string? Aciklama { get; set; }

        public bool Aktif { get; set; } = true;

        public int Sira { get; set; } = 0;
    }

    public class TedarikciEditVm : TedarikciCreateVm
    {
        public int TedarikciID { get; set; }
    }
}
