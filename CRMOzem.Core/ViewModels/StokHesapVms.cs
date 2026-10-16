using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HastaneStokPaneliVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public DateTime? SonSayimTarihi { get; set; }
        public int SonSayimNo { get; set; }
        public int? GunlerdenSayimdanBeri => SonSayimTarihi.HasValue
            ? (int)(DateTime.Now - SonSayimTarihi.Value).TotalDays
            : null;

        // Üst KPI
        public int ToplamUrun { get; set; }
        public int KritikSayisi { get; set; }
        public int DusukSayisi { get; set; }
        public int IzlemedeSayisi { get; set; }
        public int SaglikliSayisi { get; set; }
        public int VeriYokSayisi { get; set; }

        // Önerilen sipariş toplamı
        public int OnerilenToplamTest => Satirlar.Sum(s => s.OnerilenTest);
        public int OnerilenToplamKutu => Satirlar.Sum(s => s.OnerilenKutu);

        public int HedefRunwayAy { get; set; } = 3;

        public List<StokHesapSatirVm> Satirlar { get; set; } = new();
    }

    /// <summary>Tek bir ürün için (hastane × test) hesaplama + öneri.</summary>
    public class StokHesapSatirVm
    {
        // Ürün bilgisi
        public int? TestID { get; set; }
        public string TestAdi { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public string ProductName { get; set; } = "";
        public string? Platform { get; set; }
        public UrunTipi UrunTipi { get; set; }

        // Baseline — son fiziki sayım
        public DateTime? SonSayimTarihi { get; set; }
        public int SonSayimNo { get; set; }
        public int SonSayimTestCount { get; set; }

        // Delta — sayımdan sonra olanlar
        public int SayimdanSonraGonderim { get; set; }
        public int SayimdanSonraKullanim { get; set; }
        public int HesapGuncelStok { get; set; }

        // Tüketim trendi
        public int Son3AyKullanim { get; set; }
        public decimal OrtalamaAylikKullanim { get; set; }
        public int RunwayGun { get; set; }   // kaç gün yetecek

        // Öneri
        public int HedefStok { get; set; }   // 3 ay × ortalama
        public int OnerilenTest { get; set; }
        public int OnerilenKutu { get; set; }
        public int BoxTextCount { get; set; }

        public StokDurumu Durum { get; set; }

        /// <summary>"150 + 50 − 100 = 100 test · 3 aylık ort. 40/ay · 75 gün yetecek" gibi insan okur özet</summary>
        public string HesaplamaOzeti { get; set; } = "";

        // Uyarılar (data inconsistency vb.)
        public List<string> Uyarilar { get; set; } = new();
    }
}
