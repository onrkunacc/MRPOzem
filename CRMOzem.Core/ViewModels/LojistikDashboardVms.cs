using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class LojistikDashboardVm
    {
        public DashboardKpiVm Kpi { get; set; } = new();
        public List<HastaneSagligiSatirVm> Hastaneler { get; set; } = new();
        public List<VeriSagligiUyarisiVm> Uyarilar { get; set; } = new();
        public List<SonAktiviteVm> SonAktiviteler { get; set; } = new();
        public DateTime HesaplamaZamani { get; set; } = DateTime.Now;
    }

    // ═══════════════════════════════════════════════════════════
    //  KPI
    // ═══════════════════════════════════════════════════════════
    public class DashboardKpiVm
    {
        public int BekleyenTalep { get; set; }
        public int KritikKalemBekliyor { get; set; }   // en az bir Kırmızı kalemi olan bekleyen talep
        public int BuHaftaOnaylanan { get; set; }
        public int BuHaftaReddedilen { get; set; }
        public int AktifHastaneSayisi { get; set; }    // son 30 gün sipariş veren
        public int Son30GunToplamOnayTest { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  HASTANE SAĞLIĞI
    // ═══════════════════════════════════════════════════════════
    public enum HastaneSaglikDurumu
    {
        Normal = 0,
        Dikkat = 1,       // sayım 120+ gün veya en az 1 sarı bekliyor
        Kritik = 2        // sayım 180+ gün veya en az 1 kırmızı bekliyor
    }

    public class HastaneSagligiSatirVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";

        public DateTime? SonSayimTarihi { get; set; }
        public int GunlerdenSayim => SonSayimTarihi.HasValue
            ? (int)(DateTime.Now - SonSayimTarihi.Value).TotalDays
            : -1;

        public int BekleyenTalepSayisi { get; set; }
        public int BekleyenKritikKalemSayisi { get; set; }
        public DogrulamaRengi EnKotuRenk { get; set; } = DogrulamaRengi.VeriYok;

        public DateTime? SonOnayTarihi { get; set; }
        public int GunlerdenSonOnay => SonOnayTarihi.HasValue
            ? (int)(DateTime.Now - SonOnayTarihi.Value).TotalDays
            : -1;

        public HastaneSaglikDurumu Durum { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  VERİ SAĞLIĞI UYARILARI
    // ═══════════════════════════════════════════════════════════
    public enum UyariTuru
    {
        EskiSayim = 1,
        SayimYok = 2,
        NegatifStok = 3,
        EslesmeyeTest = 4,
        EslesmeyenMusteri = 5
    }

    public class VeriSagligiUyarisiVm
    {
        public UyariTuru Tur { get; set; }
        public string Baslik { get; set; } = "";
        public string Aciklama { get; set; } = "";
        public int Adet { get; set; }
        public string? AksiyonUrl { get; set; }
        public string? AksiyonText { get; set; }
        public string Seviye { get; set; } = "warn"; // "warn" veya "danger"
    }

    // ═══════════════════════════════════════════════════════════
    //  SON AKTİVİTELER
    // ═══════════════════════════════════════════════════════════
    public class SonAktiviteVm
    {
        public int TalepID { get; set; }
        public string HospitalName { get; set; } = "";
        public SiparisTalepDurumu Durum { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? Kullanici { get; set; }
        public string? Not { get; set; }
        public int KalemSayisi { get; set; }
        public int ToplamOnaylananTest { get; set; }
    }
}
