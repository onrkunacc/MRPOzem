using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class KotaHareket
    {
        public int KotaHareketID { get; set; }

        public int KotaID { get; set; }
        public Kota? Kota { get; set; }

        public int TedarikciID { get; set; }
        public Tedarikci? Tedarikci { get; set; }
        public int? HospitalID { get; set; }
        public Hospital? Hospital { get; set; }

        public KotaHareketTuru HareketTuru { get; set; }

        /// <summary>Fatura ay/yıl — filtre için</summary>
        public int Yil { get; set; }

        [Range(1, 12)]
        public int Ay { get; set; }

        [StringLength(100)]
        public string? FaturaNo { get; set; }

        public DateTime? FaturaTarihi { get; set; }

        /// <summary>Reaktif, Kalibratör, Kontrol, Sarf, Kira, Servis, Consumables vb.</summary>
        [StringLength(200)]
        public string? Aciklama { get; set; }

        [Required]
        public decimal Tutar { get; set; }

        /// <summary>USD veya TRY</summary>
        [Required, StringLength(5)]
        public string ParaBirimi { get; set; } = "TRY";

        /// <summary>Kullanılan kur (USD için TCMB'den çekilen, TRY için 1)</summary>
        public decimal Kur { get; set; } = 1;

        /// <summary>Hesaplanmış TL karşılığı: Tutar * Kur</summary>
        public decimal TLKarsiligi { get; set; }

        /// <summary>Hesaplanmış USD karşılığı: TRY ise Tutar/Kur, USD ise Tutar</summary>
        public decimal UsdKarsiligi { get; set; }

        public string? EkleyenKullaniciId { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellemeTarihi { get; set; }


    }
}
