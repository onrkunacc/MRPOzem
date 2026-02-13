using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Kurum
    {
        public int Id { get; set; }

        public string Ad { get; set; } = null!;

        public DateTime? IhaleTarihi { get; set; }
        public string? IhaleNumarasi { get; set; }

        public bool AktifMi { get; set; } = true;

        public ICollection<KurumIstatistikAylik> IstatistikAyliklar { get; set; } = new List<KurumIstatistikAylik>();
    }
}
