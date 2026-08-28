using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMOzem.Core.Models
{
    public class Kota
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string KotaKodu { get; set; } = string.Empty;

        [Required, MaxLength(250)]
        public string KotaAdi { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProformaUsd { get; set; }

        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellenmeTarihi { get; set; }

        public ICollection<KotaFinansKaydi> FinansKayitlari { get; set; } = new List<KotaFinansKaydi>();
        public ICollection<KotaCari> KotaCariler { get; set; } = new List<KotaCari>();
    }
}
