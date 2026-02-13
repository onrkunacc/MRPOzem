using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class AddKurumRequest
    {
        public string Ad { get; set; } = null!;
        public DateTime? IhaleTarihi { get; set; }
        public string? IhaleNumarasi { get; set; }
    }
}
