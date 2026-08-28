using CRMOzem.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMOzem.Core.Models
{
    public class KotaFinansKaydi
    {
        public int Id { get; set; }
        public int KotaId { get; set; }
        public Kota Kota { get; set; } = null!;

        public int DonemYil { get; set; }
        public int DonemAy { get; set; }
        public KotaKayitTipi KayitTipi { get; set; }
        public KotaTedarikciGrubu TedarikciGrubu { get; set; }
        public KotaGiderKategorisi GiderKategorisi { get; set; }
        public KotaParaBirimi ParaBirimi { get; set; }
        public KotaKayitKaynagi KayitKaynagi { get; set; } = KotaKayitKaynagi.Manuel;

        [Required, MaxLength(100)]
        public string FaturaNo { get; set; } = string.Empty;

        public DateTime FaturaTarihi { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tutar { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal DovizKuru { get; set; } = 1m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TutarTry { get; set; }

        [MaxLength(1000)]
        public string? Aciklama { get; set; }

        [MaxLength(500)]
        public string? BelgeLinki { get; set; }

        public int? KaynakFaturaId { get; set; }
        public Invoices? KaynakFatura { get; set; }

        [MaxLength(450)]
        public string? OlusturanKullaniciId { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellenmeTarihi { get; set; }
    }
}
