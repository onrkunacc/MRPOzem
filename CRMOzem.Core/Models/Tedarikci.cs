using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Tedarikci
    {
        public int TedarikciID { get; set; }

        [Required, StringLength(150)]
        public string Ad { get; set; } = "";

        public TedarikciGrup Grup { get; set; } = TedarikciGrup.Diger;

        [StringLength(500)]
        public string? Aciklama { get; set; }

        public bool Aktif { get; set; } = true;

        public int Sira { get; set; } = 0;

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<KotaHareket> Hareketler { get; set; } = new List<KotaHareket>();
    }
}
