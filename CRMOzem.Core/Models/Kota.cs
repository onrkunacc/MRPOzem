using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Kota
    {
        public int KotaID { get; set; }

        /// <summary>Proforma/Kota No — Q-00400577 gibi</summary>
        [StringLength(50)]
        public string? KotaKodu { get; set; }

        [Required, StringLength(200)]
        public string IsAdi { get; set; } = "";

        /// <summary>Bölge: İzmir, Aydın, Isparta, Denizli, Burdur, Manisa</summary>
        [StringLength(50)]
        public string? Bolge { get; set; }

        public DateTime? ProformaTarihi { get; set; }

        /// <summary>Proforma tutarı — USD bazında</summary>
        public decimal? ProformaTutarUsd { get; set; }

        public DateTime? BaslamaTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }

        /// <summary>İhale süresi (ay) — manuel girilir veya tarihlerden hesaplanır</summary>
        public int? AySayisi { get; set; }

        public KotaDurum Durum { get; set; } = KotaDurum.YeniBaslayacak;

        /// <summary>Sorumlu kişi (Aslı, Sedat, Erkan vb.)</summary>
        [StringLength(100)]
        public string? Sorumlu { get; set; }

        [StringLength(1000)]
        public string? Aciklama { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellemeTarihi { get; set; }

        public string? EkleyenKullaniciId { get; set; }

        // ── Hesaplanan (mapped değil) ──

        /// <summary>Aylık USD tutar (Proforma / AySayisi)</summary>
        public decimal? AylikTutarUsd
        {
            get
            {
                if (!ProformaTutarUsd.HasValue || !AySayisi.HasValue || AySayisi.Value <= 0)
                    return null;
                return Math.Round(ProformaTutarUsd.Value / AySayisi.Value, 2);
            }
        }

        // Navigation
        public ICollection<KotaHareket> Hareketler { get; set; } = new List<KotaHareket>();
        public ICollection<KotaHospital> Hastaneler { get; set; } = new List<KotaHospital>();  

    }
}
