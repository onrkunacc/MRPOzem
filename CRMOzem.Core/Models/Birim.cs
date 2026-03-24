using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Birim
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string BirimKodu { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string BirimAdi { get; set; } = string.Empty;

        public bool Aktif { get; set; } = true;

        // Navigation
        public ICollection<PersonelGider> PersonelGiderler { get; set; } = new List<PersonelGider>();
        public ICollection<EkGider> EkGiderler { get; set; } = new List<EkGider>();
        public ICollection<IhaleKalem> IhaleKalemler { get; set; } = new List<IhaleKalem>();
    }
}
