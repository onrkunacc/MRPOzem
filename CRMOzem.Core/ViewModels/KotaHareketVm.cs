using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.ViewModels
{
    public class KotaDetayVm
    {
        public int KotaID { get; set; }
        public string? KotaKodu { get; set; }
        public string IsAdi { get; set; } = "";
        public string? Bolge { get; set; }
        public string? Sorumlu { get; set; }
        public string? Aciklama { get; set; }

        public decimal? ProformaTutarUsd { get; set; }
        public DateTime? ProformaTarihi { get; set; }
        public DateTime? BaslamaTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public int? AySayisi { get; set; }
        public decimal? AylikTutarUsd { get; set; }
        public KotaDurum Durum { get; set; }

        // ── Hesaplanan ──
        public decimal ToplamGiderUsd { get; set; }
        public decimal ToplamGiderTry { get; set; }
        public decimal ToplamGelirUsd { get; set; }
        public decimal ToplamGelirTry { get; set; }

        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal DigerGiderUsd { get; set; }

        public decimal? KalanUsd => ProformaTutarUsd.HasValue ? ProformaTutarUsd - ToplamGiderUsd : null;
        public decimal? KullanimYuzdesi
        {
            get
            {
                if (!ProformaTutarUsd.HasValue || ProformaTutarUsd.Value <= 0) return null;
                return Math.Round((ToplamGiderUsd / ProformaTutarUsd.Value) * 100, 1);
            }
        }

        public decimal? KarZararUsd => ToplamGelirUsd - ToplamGiderUsd;

        public string DurumText => Durum switch
        {
            KotaDurum.YeniBaslayacak => "Yeni Başlayacak",
            KotaDurum.Onayda => "Onayda",
            KotaDurum.Bekleyen => "Bekleyen",
            KotaDurum.Aktif => "Aktif",
            KotaDurum.Kapandi => "Kapandı",
            _ => "—"
        };

        public List<KotaHareketRowVm> Hareketler { get; set; } = new();
        public List<AylikOzetRowVm> AylikOzet { get; set; } = new();
        public List<KotaHospitalOzetVm> HastaneOzet { get; set; } = new();

    }
    // ═══ YENİ CLASS ═══
    public class KotaHospitalOzetVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public int HareketSayisi { get; set; }
        public decimal ToplamGiderUsd { get; set; }
        public decimal ToplamGelirUsd { get; set; }
        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal KarZararUsd => ToplamGelirUsd - ToplamGiderUsd;
    }
    public class KotaHareketRowVm
    {
        public int KotaHareketID { get; set; }
        public KotaHareketTuru HareketTuru { get; set; }
        public int TedarikciID { get; set; }
        public string TedarikciAd { get; set; } = "";
        public TedarikciGrup TedarikciGrup { get; set; }
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string? FaturaNo { get; set; }
        public DateTime? FaturaTarihi { get; set; }
        public string? Aciklama { get; set; }
        public decimal Tutar { get; set; }
        public string ParaBirimi { get; set; } = "TRY";
        public decimal Kur { get; set; }
        public decimal TLKarsiligi { get; set; }
        public decimal UsdKarsiligi { get; set; }
        public int? HospitalID { get; set; }
        public string? HospitalName { get; set; }
    }

    public class AylikOzetRowVm
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string AyLabel { get; set; } = "";
        public decimal AbbottGiderUsd { get; set; }
        public decimal ThirdPartyGiderUsd { get; set; }
        public decimal DigerGiderUsd { get; set; }
        public decimal ToplamGiderUsd { get; set; }
        public decimal GelirUsd { get; set; }
        public decimal KarZararUsd => GelirUsd - ToplamGiderUsd;
    }

    public class KotaHareketCreateVm
    {
        [Required]
        public int KotaID { get; set; }

        [Required(ErrorMessage = "Hareket türü zorunlu")]
        public KotaHareketTuru HareketTuru { get; set; } = KotaHareketTuru.Gider;

        [Required(ErrorMessage = "Tedarikçi seçiniz")]
        public int TedarikciID { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FaturaTarihi { get; set; } = DateTime.Today;

        [StringLength(100)]
        public string? FaturaNo { get; set; }

        [StringLength(200)]
        public string? Aciklama { get; set; }

        [Required(ErrorMessage = "Tutar zorunlu")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalı")]
        public decimal Tutar { get; set; }

        [Required]
        [StringLength(5)]
        public string ParaBirimi { get; set; } = "USD";

        /// <summary>Kullanıcı manuel değiştirebilir</summary>
        public decimal? Kur { get; set; }
        public int? HospitalID { get; set; }
    }

    public class KotaHareketEditVm : KotaHareketCreateVm
    {
        public int KotaHareketID { get; set; }
    }

    /// <summary>AJAX: Kur bilgi kutucuğu</summary>
    public class KurBilgiVm
    {
        public bool Basarili { get; set; }
        public decimal? Kur { get; set; }
        public DateTime? KurTarihi { get; set; }
        public string? Mesaj { get; set; }
    }
}