using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    [Table("FizikiSayimSnapshot", Schema = "ozem")]
    public class FizikiSayimSnapshot
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [ForeignKey(nameof(HospitalID))]
        public Hospital? Hospital { get; set; }

        /// <summary>crm.ozemmedikal.com'daki ProductID (bizim DB'mizde Product tablosu yok, sadece ID + ad)</summary>
        [Required]
        public int ExternalProductId { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; } = "";

        [Required, StringLength(50)]
        public string ReferenceNumber { get; set; } = "";

        [StringLength(100)]
        public string? ProductType { get; set; }   // "Architect Küçük Kutu", "Alinity Sarf" vs.

        [StringLength(100)]
        public string? Platform { get; set; }      // "Architect", "Alinity"

        /// <summary>Bizim türettiğimiz ürün tipi — tespit kurallarına göre</summary>
        public CRMOzem.Core.Enums.UrunTipi UrunTipi { get; set; }

        /// <summary>Eğer bu bir reaktifse ve Test tablosuyla eşleşiyorsa (ReferenceNumber == Test.SutCode)</summary>
        public int? MappedTestID { get; set; }

        [ForeignKey(nameof(MappedTestID))]
        public Test? MappedTest { get; set; }

        // ─── Sayım bilgileri (API'den) ───
        [Required]
        public int SayimNo { get; set; }

        public DateTime SayimTarihi { get; set; }

        public bool SayimAktifMi { get; set; }

        // ─── Stok rakamları ───
        public int BoxCount { get; set; }        // kutu adedi
        public int FlaconCount { get; set; }     // flakon adedi
        public int TestCount { get; set; }       // normalize edilmiş test adedi (ana doğruluk kaynağı)

        public int BoxTextCount { get; set; }    // 1 kutu kaç test
        public int FlaconCountPerBox { get; set; } // 1 kutu kaç flakon

        [StringLength(100)]
        public string? LotNumber { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public DateTime LastUpdatedExternal { get; set; }  // API'deki LastUpdated

        /// <summary>Bu snapshot'ın CRMOzem'e geldiği zaman</summary>
        public DateTime CekilmeTarihi { get; set; } = DateTime.Now;
    }
}
