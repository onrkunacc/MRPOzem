using CRMOzem.Core.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels.Dogrulama
{
    public class DogrulamaIndexVm
    {
        public List<DogrulamaListSatirVm> Satirlar { get; set; } = new();
        public DogrulamaFiltreVm Filtre { get; set; } = new();

        // Üst özet rozetleri
        public int ToplamBekleyen { get; set; }
        public int ToplamOnaylananAynen { get; set; }
        public int ToplamOnaylananDuzeltildi { get; set; }
        public int ToplamReddedilen { get; set; }
        public int ToplamKirmizi { get; set; }     // bekleyenler içinde en az bir kalemi kırmızı olan
        public int ToplamSari { get; set; }
    }

    public class DogrulamaListSatirVm
    {
        public int TalepID { get; set; }
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public string? HedefDonem { get; set; }         // "2026-10" vb.
        public DateTime TalepTarihi { get; set; }
        public string? ExternalRefNo { get; set; }      // PHP sipariş id
        public SiparisTalepDurumu Durum { get; set; }

        public int KalemSayisi { get; set; }
        public int ToplamTalepTest { get; set; }
        public int ToplamOneriTest { get; set; }

        // Renk dağılımı
        public DogrulamaRengi EnKotuRenk { get; set; } = DogrulamaRengi.VeriYok;
        public int KirmiziSayisi { get; set; }
        public int SariSayisi { get; set; }
        public int YesilSayisi { get; set; }
        public int VeriYokSayisi { get; set; }

        public string? HastaneNotu { get; set; }

        // Türetilmiş
        public int ToplamFark => ToplamOneriTest - ToplamTalepTest;
        public decimal FarkYuzde => ToplamTalepTest > 0
            ? ((decimal)(ToplamOneriTest - ToplamTalepTest) / ToplamTalepTest) * 100m
            : 0m;
    }

    public class DogrulamaFiltreVm
    {
        public string? Donem { get; set; }
        public SiparisTalepDurumu? Durum { get; set; } = SiparisTalepDurumu.Beklemede;
        public DogrulamaRengi? EnKotuRenk { get; set; }
        public int? HospitalID { get; set; }

        public List<SelectListItem> Donemler { get; set; } = new();
        public List<SelectListItem> Hastaneler { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    //  DETAY
    // ═══════════════════════════════════════════════════════════
    public class DogrulamaDetayVm
    {
        public int TalepID { get; set; }
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public string? HedefDonem { get; set; }
        public DateTime TalepTarihi { get; set; }
        public string? ExternalRefNo { get; set; }
        public string? HastaneNotu { get; set; }
        public SiparisTalepDurumu Durum { get; set; }

        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }
        public string? OnayNotu { get; set; }

        public List<DogrulamaKalemVm> Kalemler { get; set; } = new();

        // Toplam özet
        public int ToplamTalepTest => Kalemler.Sum(k => k.HastaneTalepTest);
        public int ToplamOneriTest => Kalemler.Sum(k => k.OnerilenTest ?? 0);
        public int KirmiziSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Kirmizi);
        public int SariSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Sari);
        public int YesilSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Yesil);
        public int VeriYokSayisi => Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.VeriYok);
    }

    public class DogrulamaKalemVm
    {
        public int KalemID { get; set; }

        // Ürün
        public string ExternalProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string? ReferenceNumber { get; set; }
        public int? TestID { get; set; }
        public UrunTipi UrunTipi { get; set; }

        // Hastane talebi
        public int HastaneTalepTest { get; set; }
        public decimal? HastaneTalepKutu { get; set; }

        // Hesap
        public decimal? HesapGuncelStok { get; set; }
        public decimal? OrtalamaAylikKullanim { get; set; }

        // Öneri
        public int? OnerilenTest { get; set; }
        public decimal? OnerilenKutu { get; set; }
        public decimal? SapmaYuzde { get; set; }
        public DogrulamaRengi DogrulamaRengi { get; set; }
        public string? HesaplamaAciklamasi { get; set; }

        // Onay — düzenlenebilir
        public int? OnaylananTest { get; set; }
        public string? KalemNotu { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  ONAY POST DTOs
    // ═══════════════════════════════════════════════════════════
    public class OnayPostDto
    {
        public int TalepID { get; set; }
        public string? OnayNotu { get; set; }
        public List<KalemOnayDto> Kalemler { get; set; } = new();
    }

    public class KalemOnayDto
    {
        public int KalemID { get; set; }
        public int? OnaylananTest { get; set; }
        public string? KalemNotu { get; set; }
    }

    public class RedPostDto
    {
        public int TalepID { get; set; }
        public string? RedNedeni { get; set; }
    }
}
