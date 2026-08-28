using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.ViewModels.KotaTakip
{
    public class KotaFinansKaydiInputViewModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Kota seçiniz.")]
        [Display(Name = "Kota")]
        public int KotaId { get; set; }

        [Range(2020, 2100, ErrorMessage = "Geçerli bir yıl giriniz.")]
        [Display(Name = "Yıl")]
        public int DonemYil { get; set; } = DateTime.Now.Year;

        [Range(1, 12, ErrorMessage = "Ay seçiniz.")]
        [Display(Name = "Ay")]
        public int DonemAy { get; set; } = DateTime.Now.Month;

        [Display(Name = "Kayıt Türü")]
        public KotaKayitTipi KayitTipi { get; set; } = KotaKayitTipi.Gider;

        [Display(Name = "Tedarikçi Grubu")]
        public KotaTedarikciGrubu TedarikciGrubu { get; set; } = KotaTedarikciGrubu.Abbott;

        [Display(Name = "Gider Kategorisi")]
        public KotaGiderKategorisi GiderKategorisi { get; set; } = KotaGiderKategorisi.Reaktif;

        [Display(Name = "Para Birimi")]
        public KotaParaBirimi ParaBirimi { get; set; } = KotaParaBirimi.USD;

        [Required(ErrorMessage = "Fatura numarası zorunludur."), MaxLength(100)]
        [Display(Name = "Fatura No")]
        public string FaturaNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Fatura tarihi zorunludur."), DataType(DataType.Date)]
        [Display(Name = "Fatura Tarihi")]
        public DateTime FaturaTarihi { get; set; } = DateTime.Today;

        [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
        public decimal Tutar { get; set; }

        [Range(typeof(decimal), "0", "999999", ErrorMessage = "Kur negatif olamaz.")]
        [Display(Name = "Döviz Kuru")]
        public decimal DovizKuru { get; set; } = 1m;

        [MaxLength(1000)]
        [Display(Name = "Açıklama")]
        public string? Aciklama { get; set; }

        [MaxLength(500), Url(ErrorMessage = "Belge bağlantısı geçerli bir adres olmalıdır.")]
        [Display(Name = "Belge Bağlantısı")]
        public string? BelgeLinki { get; set; }
    }

    public class KotaInputViewModel
    {
        [Required(ErrorMessage = "Kota kodu zorunludur."), MaxLength(50)]
        [Display(Name = "Kota Kodu")]
        public string KotaKodu { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kota adı zorunludur."), MaxLength(250)]
        [Display(Name = "Kota Adı")]
        public string KotaAdi { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Proforma tutarı negatif olamaz.")]
        [Display(Name = "Proforma (USD)")]
        public decimal ProformaUsd { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Başlangıç")]
        public DateTime? BaslangicTarihi { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Bitiş")]
        public DateTime? BitisTarihi { get; set; }
    }

    public class KotaCariInputViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Kota seçiniz.")]
        [Display(Name = "Kota")]
        public int KotaId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Cari seçiniz.")]
        [Display(Name = "Cari")]
        public int CariId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Başlangıç")]
        public DateTime? BaslangicTarihi { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Bitiş")]
        public DateTime? BitisTarihi { get; set; }
    }

    public class KotaTakipIndexViewModel
    {
        public KotaFinansKaydiInputViewModel Kayit { get; set; } = new();
        public KotaInputViewModel YeniKota { get; set; } = new();
        public KotaCariInputViewModel YeniKotaCari { get; set; } = new();
        public IReadOnlyList<Kota> Kotalar { get; set; } = Array.Empty<Kota>();
        public IReadOnlyList<Cariler> Cariler { get; set; } = Array.Empty<Cariler>();
        public IReadOnlyList<KotaFinansKaydi> Kayitlar { get; set; } = Array.Empty<KotaFinansKaydi>();
        public int SecilenYil { get; set; }
        public int? SecilenAy { get; set; }
        public bool DuzenlemeModu => Kayit.Id > 0;
    }

    public class KotaOzetSatirViewModel
    {
        public int KotaId { get; set; }
        public string KotaKodu { get; set; } = string.Empty;
        public string KotaAdi { get; set; } = string.Empty;
        public decimal ProformaUsd { get; set; }
        public decimal AbbottUsd { get; set; }
        public decimal UcuncuPartiTry { get; set; }
        public decimal ToplamGiderTry { get; set; }
        public decimal ToplamGelirTry { get; set; }
        public decimal FinansalFarkTry => ToplamGelirTry - ToplamGiderTry;
        public decimal ProformaKullanimOrani => ProformaUsd > 0 ? AbbottUsd / ProformaUsd * 100m : 0m;
        public Dictionary<KotaGiderKategorisi, decimal> KategoriToplamlari { get; set; } = new();
    }

    public class KotaOzetViewModel
    {
        public int SecilenYil { get; set; }
        public int? SecilenAy { get; set; }
        public IReadOnlyList<KotaOzetSatirViewModel> Satirlar { get; set; } = Array.Empty<KotaOzetSatirViewModel>();
        public decimal ToplamGiderTry => Satirlar.Sum(x => x.ToplamGiderTry);
        public decimal ToplamGelirTry => Satirlar.Sum(x => x.ToplamGelirTry);
        public decimal ToplamUcuncuPartiTry => Satirlar.Sum(x => x.UcuncuPartiTry);
        public decimal ToplamFinansalFarkTry => ToplamGelirTry - ToplamGiderTry;
    }

    public class KotaSenkronizasyonSonucu
    {
        public int Eklenen { get; set; }
        public int Guncellenen { get; set; }
        public int Kaldirilan { get; set; }
        public int Eslestirilemeyen { get; set; }
    }
}
