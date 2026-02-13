using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class KurumRowVm
    {
        public int KurumId { get; set; }
        public string KurumAdi { get; set; } = null!;
        public DateTime? IhaleTarihi { get; set; }
        public string? IhaleNumarasi { get; set; }

        // Seçilen aylar kadar hücre
        public List<AyDurumVm> Aylar { get; set; } = new();
    }
}
