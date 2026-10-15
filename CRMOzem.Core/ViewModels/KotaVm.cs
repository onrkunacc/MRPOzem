using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class KotaListVm
    {
        public int KotaID { get; set; }
        public string? KotaKodu { get; set; }
        public string IsAdi { get; set; } = "";
        public string? Bolge { get; set; }
        public decimal? ProformaTutarUsd { get; set; }
        public decimal? ProformaTutarTry { get; set; }
        public DateTime? ProformaTarihi { get; set; }
        public DateTime? BaslamaTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public int? AySayisi { get; set; }
        public decimal? AylikTutarUsd { get; set; }
        public KotaDurum Durum { get; set; }
        public string? Sorumlu { get; set; }
        public int HastaneSayisi { get; set; }


        // Hesaplanan
        public decimal ToplamGiderUsd { get; set; }
        public decimal ToplamGelirUsd { get; set; }
        public decimal? KalanUsd => ProformaTutarUsd.HasValue ? ProformaTutarUsd - ToplamGiderUsd : null;
        public decimal? KullanimYuzdesi
        {
            get
            {
                if (!ProformaTutarUsd.HasValue || ProformaTutarUsd.Value <= 0) return null;
                return Math.Round((ToplamGiderUsd / ProformaTutarUsd.Value) * 100, 1);
            }
        }

        /// <summary>Bugüne kadar geçen ay sayısı</summary>
        public int? GecenAy
        {
            get
            {
                if (!BaslamaTarihi.HasValue) return null;
                var bugun = DateTime.Now;
                var gecen = ((bugun.Year - BaslamaTarihi.Value.Year) * 12) + (bugun.Month - BaslamaTarihi.Value.Month);
                return Math.Max(0, gecen);
            }
        }

        public int? KalanAy
        {
            get
            {
                if (!AySayisi.HasValue || !GecenAy.HasValue) return null;
                return Math.Max(0, AySayisi.Value - GecenAy.Value);
            }
        }

        public string DurumText => Durum switch
        {
            KotaDurum.YeniBaslayacak => "Yeni Başlayacak",
            KotaDurum.Onayda => "Onayda",
            KotaDurum.Bekleyen => "Bekleyen",
            KotaDurum.Aktif => "Aktif",
            KotaDurum.Kapandi => "Kapandı",
            _ => "—"
        };
    }

    public class KotaCreateVm
    {
        [StringLength(50, ErrorMessage = "Maksimum 50 karakter")]
        public string? KotaKodu { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "İş adı zorunlu")]
        [StringLength(200)]
        public string IsAdi { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Bolge { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ProformaTarihi { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalı")]
        public decimal? ProformaTutarUsd { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BaslamaTarihi { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BitisTarihi { get; set; }

        [Range(1, 120, ErrorMessage = "1-120 ay arasında olmalı")]
        public int? AySayisi { get; set; }

        [Required]
        public KotaDurum Durum { get; set; } = KotaDurum.YeniBaslayacak;

        [StringLength(100)]
        public string? Sorumlu { get; set; }

        [StringLength(1000)]
        public string? Aciklama { get; set; }
        public List<int> SecilenHastaneIdler { get; set; } = new();
    }

    public class KotaEditVm : KotaCreateVm
    {
        public int KotaID { get; set; }
    }

    public class KotaFilterVm
    {
        public string? Search { get; set; }
        public string? Bolge { get; set; }
        public KotaDurum? Durum { get; set; }
    }
}
