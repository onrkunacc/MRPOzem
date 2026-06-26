using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.ViewModels
{
    // Ana sayfa view model'i
    public class HospitalBirimPageVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = string.Empty;

        // Aktif kayıtlar (her birim için olabilir, birden fazla bile)
        public List<HospitalBirimRowVm> AktifKayitlar { get; set; } = new();

        // Pasif/geçmiş kayıtlar
        public List<HospitalBirimRowVm> PasifKayitlar { get; set; } = new();

        // Henüz kayıt eklenmemiş birimler (hızlı ekleme için)
        public List<BirimMiniVm> EklenebilirBirimler { get; set; } = new();
    }

    public class HospitalBirimRowVm
    {
        public int Id { get; set; }
        public int BirimId { get; set; }
        public string BirimKodu { get; set; } = string.Empty;
        public string BirimAdi { get; set; } = string.Empty;
        public bool Aktif { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? IhaleKayitNo { get; set; }
        public string? Aciklama { get; set; }
        public int? KalanGun { get; set; }        // bitiş tarihine kalan gün (null = bitiş yok)
        public bool SuresiDoldu { get; set; }      // BitisTarihi geçmişte ama Aktif=true ise
        public bool SuresiYaklasti { get; set; }   // 30 günden az kaldıysa
    }

    public class BirimMiniVm
    {
        public int Id { get; set; }
        public string BirimKodu { get; set; } = string.Empty;
        public string BirimAdi { get; set; } = string.Empty;
    }

    // Ekleme/düzenleme formu
    public class HospitalBirimFormVm
    {
        public int Id { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [Required(ErrorMessage = "Birim seçimi zorunludur.")]
        public int BirimId { get; set; }

        public bool Aktif { get; set; } = true;

        [DataType(DataType.Date)]
        public DateTime? BaslangicTarihi { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BitisTarihi { get; set; }

        [MaxLength(50)]
        public string? IhaleKayitNo { get; set; }

        [MaxLength(500)]
        public string? Aciklama { get; set; }
    }
    
    public class HospitalBirimOverviewVm
    {
        public int ToplamHastane { get; set; }
        public int AktifEslesmeVarHastane { get; set; }
        public int ToplamAktifEslesme { get; set; }
        public int SuresiYaklasanSayisi { get; set; }
        public int SuresiDolmusAktifSayisi { get; set; }
    }
}