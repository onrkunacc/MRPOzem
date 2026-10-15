using CRMOzem.Core.Enums;

namespace CRMOzem.Core.ViewModels
{
    // ═══════════════════════════════════════════════════════════════
    //  KotaHareket/Detay sayfasının modeli
    //  Entity'leri view'a direkt vermiyoruz — slim VM.
    // ═══════════════════════════════════════════════════════════════
    public class KotaHareketDetayVm
    {
        public KotaInfoVm Kota { get; set; } = new();
        public List<KotaHareketItemVm> Hareketler { get; set; } = new();
    }

    public class KotaInfoVm
    {
        public int KotaID { get; set; }
        public string? KotaKodu { get; set; }
        public string IsAdi { get; set; } = "";
        public string? Sorumlu { get; set; }
        public string? Bolge { get; set; }
        public DateTime? BaslamaTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public decimal ProformaTutarUsd { get; set; }
        public KotaDurum Durum { get; set; }
    }

    public class KotaHareketItemVm
    {
        public int ID { get; set; }

        /// <summary>Hareketin tarihi (fatura tarihi)</summary>
        public DateTime Tarih { get; set; }

        /// <summary>Gider / Gelir</summary>
        public KotaHareketTuru Tur { get; set; }

        public string? TedarikciAdi { get; set; }
        public TedarikciGrup? TedarikciGrup { get; set; }

        public int? HospitalID { get; set; }
        public string? HospitalName { get; set; }

        public string? Aciklama { get; set; }

        /// <summary>USD karşılığı</summary>
        public decimal TutarUsd { get; set; }

        /// <summary>TL tutarı (varsa)</summary>
        public decimal? TutarTl { get; set; }
    }
}