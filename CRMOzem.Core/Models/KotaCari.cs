using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.Models
{
    public class KotaCari
    {
        public int Id { get; set; }
        public int KotaId { get; set; }
        public Kota Kota { get; set; } = null!;

        public int CariId { get; set; }
        public Cariler Cari { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime? BaslangicTarihi { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BitisTarihi { get; set; }

        public bool Aktif { get; set; } = true;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    }
}
