using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    [Table("SiparisTalep", Schema = "ozem")]
    public class SiparisTalep
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [ForeignKey(nameof(HospitalID))]
        public Hospital? Hospital { get; set; }

        [Required]
        public DateTime TalepTarihi { get; set; } = DateTime.Now;

        /// <summary>Hangi ay için talep edildi (örn. 2026-10-01 = "Ekim ayı siparişi")</summary>
        public DateTime? HedefDonem { get; set; }

        /// <summary>Hastanenin dış sipariş yazılımındaki ID — eşlenebilsin diye</summary>
        [StringLength(100)]
        public string? ExternalRefNo { get; set; }

        [StringLength(1000)]
        public string? HastaneNotu { get; set; }

        public SiparisTalepDurumu Durum { get; set; } = SiparisTalepDurumu.Beklemede;

        // ─── Lojistik işlem bilgisi ───
        public DateTime? OnayTarihi { get; set; }

        [StringLength(100)]
        public string? OnaylayanKullanici { get; set; }

        [StringLength(1000)]
        public string? OnayNotu { get; set; }

        // ─── Audit ───
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellemeTarihi { get; set; }

        // ─── Navigation ───
        public ICollection<SiparisTalepKalem> Kalemler { get; set; } = new List<SiparisTalepKalem>();
    }

    /// <summary>
    /// Bir sipariş talebindeki tek bir ürün satırı.
    /// Hastane 500 test (3 kutu) istedi; sistem 350 (2 kutu) önerdi; lojistik 400 (2.5 kutu) onayladı.
    /// </summary>
    [Table("SiparisTalepKalem", Schema = "ozem")]
    public class SiparisTalepKalem
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int SiparisTalepID { get; set; }

        [ForeignKey(nameof(SiparisTalepID))]
        public SiparisTalep? SiparisTalep { get; set; }

        // ─── Ürün ───
        /// <summary>crm.ozemmedikal.com'daki ProductID (dış)</summary>
        [Required]
        public int ExternalProductId { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; } = "";

        [Required, StringLength(50)]
        public string ReferenceNumber { get; set; } = "";

        /// <summary>Eğer reaktif ve Test ile eşleşiyorsa</summary>
        public int? TestID { get; set; }

        [ForeignKey(nameof(TestID))]
        public Test? Test { get; set; }

        public UrunTipi UrunTipi { get; set; }

        // ─── MİKTAR (hastane tarafı) ───
        /// <summary>Hastanenin istediği miktar (test cinsinden — tek doğruluk kaynağı)</summary>
        public int HastaneTalepTest { get; set; }

        /// <summary>Hastanenin istediği miktar (kutu — gösterim için)</summary>
        public int? HastaneTalepKutu { get; set; }

        // ─── SİSTEM ÖNERİSİ ───
        /// <summary>Hesaplanan projeksiyon stoku (snapshot + gönderim - kullanım)</summary>
        public int HesapGuncelStok { get; set; }

        /// <summary>Son 3 ay ortalama aylık kullanım (test/ay)</summary>
        public decimal OrtalamaAylikKullanim { get; set; }

        /// <summary>Sistemin önerdiği miktar (test)</summary>
        public int OnerilenTest { get; set; }

        /// <summary>Önerilen kaç kutu (yuvarlanmış)</summary>
        public int OnerilenKutu { get; set; }

        /// <summary>Sapma yüzdesi: (Talep - Öneri) / Öneri × 100. Pozitifse fazla istenmiş.</summary>
        public decimal? SapmaYuzde { get; set; }

        public DogrulamaRengi DogrulamaRengi { get; set; } = DogrulamaRengi.VeriYok;

        /// <summary>Hesaplamada kullanılan açıklama — kullanıcıya "neden?" sorusuna cevap verir</summary>
        [StringLength(2000)]
        public string? HesaplamaAciklamasi { get; set; }

        // ─── ONAY (lojistik tarafı) ───
        /// <summary>Lojistiğin onayladığı final miktar (test)</summary>
        public int? OnaylananTest { get; set; }

        [StringLength(500)]
        public string? KalemNotu { get; set; }

        // ─── Audit ───
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    }
}
