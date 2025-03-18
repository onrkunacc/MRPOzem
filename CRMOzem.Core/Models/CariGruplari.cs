using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class CariGruplari
    {
        public int Id { get; set; }
        public string GrupAdi { get; set; }
        public ICollection<Cariler> Cariler { get; set; } // Navigation Property
    }
}
