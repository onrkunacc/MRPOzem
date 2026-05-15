namespace CRMOzem.Core.ViewModels.Admin
{
    public class AdminDashboardVm
    {
        // Üst KPI şeridi
        public KpiSeritVm Kpi { get; set; } = new();

        // Masraf analizi
        public MasrafAnalizVm MasrafAnaliz { get; set; } = new();

        // Aylık trend (son 12 ay)
        public List<AylikTrendVm> AylikTrend { get; set; } = new();

        // Aktif ihaleler
        public List<IhaleOzetCardVm> AktifIhaleler { get; set; } = new();

        // Hastane × Birim widget
        public HastaneBirimOzetVm HastaneBirimOzet { get; set; } = new();

        // Bekleyen onaylar
        public BekleyenOnaylarVm BekleyenOnaylar { get; set; } = new();
    }

    public class KpiSeritVm
    {
        public decimal BuAyMasraf { get; set; }
        public decimal GecenAyMasraf { get; set; }
        public decimal MasrafDegisimYuzde =>
            GecenAyMasraf == 0 ? 0 : Math.Round(((BuAyMasraf - GecenAyMasraf) / GecenAyMasraf) * 100, 1);

        public int AktifKullaniciSayisi { get; set; }
        public int YeniKullaniciBuAy { get; set; }

        public int AktifIhaleSayisi { get; set; }
        public decimal ToplamIhaleMaliyet { get; set; }

        public int AktifHastaneBirimSayisi { get; set; }
        public int FarkliHastaneSayisi { get; set; }
    }

    public class MasrafAnalizVm
    {
        public List<KullaniciHarcamaVm> KullaniciHarcamalari { get; set; } = new();
        public List<EnYuksekMasrafVm> EnYuksekMasraflar { get; set; } = new();
        public string Periyot { get; set; } = "ay"; // ay, ceyrek, yil
    }

    public class KullaniciHarcamaVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public decimal Toplam { get; set; }
        public int IslemSayisi { get; set; }
    }

    public class EnYuksekMasrafVm
    {
        public int ExpenseId { get; set; }
        public int ReportId { get; set; }
        public string FullName { get; set; } = "";
        public string Kategori { get; set; } = "";
        public string Aciklama { get; set; } = "";
        public DateTime Tarih { get; set; }
        public decimal Tutar { get; set; }
    }

    public class AylikTrendVm
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string AyAdi { get; set; } = "";
        public decimal ToplamMasraf { get; set; }
    }

    public class IhaleOzetCardVm
    {
        public int Id { get; set; }
        public string IhaleAdi { get; set; } = "";
        public string? IhaleKayitNo { get; set; }
        public DateTime? IhaleTarihi { get; set; }
        public int IhaleSuresiGun { get; set; }
        public decimal ToplamMaliyet { get; set; }     // Personel + Ek Gider + SUT
        public decimal ToplamSutPuani { get; set; }
        public decimal MaliyetSutOrani { get; set; }   // Maliyet / SUT puanı
        public int KalemSayisi { get; set; }
    }

    public class HastaneBirimOzetVm
    {
        public List<HastaneOzetVm> EnAktifHastaneler { get; set; } = new();
        public List<BirimDagilimVm> BirimDagilim { get; set; } = new();
    }

    public class HastaneOzetVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public int AktifBirimSayisi { get; set; }
        public List<string> BirimKodlari { get; set; } = new();
    }

    public class BirimDagilimVm
    {
        public string BirimKodu { get; set; } = "";
        public string BirimAdi { get; set; } = "";
        public int HastaneSayisi { get; set; }  // Bu birimde kaç hastanede aktifiz
    }

    public class BekleyenOnaylarVm
    {
        public int BekleyenKullaniciSayisi { get; set; }
        public List<BekleyenKullaniciVm> SonBekleyenler { get; set; } = new();
    }

    public class BekleyenKullaniciVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Email { get; set; }
        public string? Title { get; set; }
    }
}