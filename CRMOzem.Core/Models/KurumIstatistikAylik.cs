using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class KurumIstatistikAylik
    {
        public int Id { get; set; }

        public int KurumId { get; set; }
        public Kurum Kurum { get; set; } = null!;

        public int Yil { get; set; }     // 2026
        public int Ay { get; set; }      // 1-12

        public bool GeldiMi { get; set; }

        public string? Not { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
