using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Core.ViewModels
{
    public class LeaveBalanceVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Title { get; set; }
        public string? Departman { get; set; }
        public int Year { get; set; }

        public DateTime? IseGirisTarihi { get; set; }
        public DateTime? DogumTarihi { get; set; }

        /// <summary>Bu yılın hakedişi.</summary>
        public int TotalDays { get; set; }

        /// <summary>Geçen yıldan devreden.</summary>
        public int CarriedOverDays { get; set; }

        /// <summary>Onaylanmış izinlerin toplam günü.</summary>
        public int UsedDays { get; set; }

        /// <summary>
        /// Henüz karara bağlanmamış taleplerin toplam günü —
        /// hem yönetici onayında hem İK onayında olanlar.
        /// </summary>
        public int PendingDays { get; set; }

        /// <summary>İK elle mi girdi, sistem mi hesapladı?</summary>
        public bool ManuelGirildi { get; set; }

        /// <summary>İK bu yıl için hakediş kaydı açmış mı?</summary>
        public bool HasRecord { get; set; }

        /// <summary>Yıl sonu itibarıyla kıdem.</summary>
        public int KidemYili { get; set; }

        /// <summary>Kıdeme göre sistemin önerdiği hakediş.</summary>
        public int OnerilenHakedis { get; set; }

        /// <summary>Öneri hangi kurala dayanıyor.</summary>
        public string? HakedisAciklamasi { get; set; }

        public int Entitlement => TotalDays + CarriedOverDays;
        public int RemainingDays => Entitlement - UsedDays;
        public int ProjectedRemainingDays => Entitlement - UsedDays - PendingDays;

        public decimal UsagePercent => Entitlement > 0
            ? Math.Round((decimal)UsedDays / Entitlement * 100, 1)
            : 0m;

        /// <summary>Girilen hakediş kanuni asgariden düşük mü?</summary>
        public bool AsgariAltinda => HasRecord && OnerilenHakedis > 0 && TotalDays < OnerilenHakedis;

        /// <summary>İşe giriş tarihi girilmemiş — otomatik hesaplanamıyor.</summary>
        public bool IseGirisEksik => IseGirisTarihi == null;
    }

    // ═══════════════════════════════════════════════════════════
    //  ÇALIŞAN EKRANLARI
    // ═══════════════════════════════════════════════════════════

    public class LeaveListViewModel
    {
        public List<LeaveRequest> Leaves { get; set; } = new();
        public LeaveBalanceVm Balance { get; set; } = new();
        public int Year { get; set; } = DateTime.Now.Year;
        public List<int> AvailableYears { get; set; } = new();

        /// <summary>Çalışanın yöneticisinin adı — "talebiniz kime gidiyor" bilgisi.</summary>
        public string? YoneticiAdi { get; set; }

        /// <summary>Bu kullanıcı birilerinin yöneticisi mi? (ekip onay kutusu için)</summary>
        public bool YoneticiMi { get; set; }

        /// <summary>Ekibinde kendi kararını bekleyen talep sayısı.</summary>
        public int EkipBekleyenSayisi { get; set; }

        /// <summary>Geriye dönük uyumluluk.</summary>
        public int RemainingDays => Balance.RemainingDays;
    }

    public class CreateLeaveRequestViewModel
    {
        [Display(Name = "İzin Türü")]
        [Range(1, int.MaxValue, ErrorMessage = "Lütfen izin türü seçiniz.")]
        public int LeaveTypeId { get; set; }

        [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [Display(Name = "Başlangıç")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Bitiş tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [Display(Name = "Bitiş")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        public TimeSpan? StartHour { get; set; }
        public TimeSpan? EndHour { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Açıklama")]
        public string? Reason { get; set; }

        [Display(Name = "Cumartesi sayılmasın")]
        public bool SaturdayExcluded { get; set; } = false;

        public IEnumerable<SelectListItem> LeaveTypes { get; set; } = Enumerable.Empty<SelectListItem>();

        public LeaveBalanceVm Balance { get; set; } = new();

        /// <summary>İzin türü id → yıllık haktan düşer mi.</summary>
        public Dictionary<int, bool> QuotaDeductingTypes { get; set; } = new();

        /// <summary>Takvimde işaretlenecek resmi tatiller (yyyy-MM-dd → ad).</summary>
        public Dictionary<string, string> Tatiller { get; set; } = new();

        /// <summary>Talep önce kime gidecek — yöneticisi yoksa null.</summary>
        public string? YoneticiAdi { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  İK — TALEP LİSTESİ
    // ═══════════════════════════════════════════════════════════

    public class LeaveAdminIndexVm
    {
        public List<LeaveRequest> Requests { get; set; } = new();
        public int Year { get; set; } = DateTime.Now.Year;
        public List<int> AvailableYears { get; set; } = new();

        /// <summary>
        /// Pending (yönetici onayında) / SupervisorApproved (İK onayında) /
        /// Approved / Rejected / Cancelled / "acik" (ikisi birlikte) / null (tümü)
        /// </summary>
        public string? StatusCode { get; set; } = "SupervisorApproved";

        /// <summary>1. aşamada — yöneticisinin kararını bekleyenler.</summary>
        public int YoneticiOnayindaCount { get; set; }

        /// <summary>2. aşamada — İK'nın kararını bekleyenler. İK'nın iş kuyruğu bu.</summary>
        public int IkOnayindaCount { get; set; }

        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int CancelledCount { get; set; }

        public int AcikCount => YoneticiOnayindaCount + IkOnayindaCount;
        public int TotalCount => AcikCount + ApprovedCount + RejectedCount + CancelledCount;

        public int ApprovedDays { get; set; }
        public int TodayOnLeaveCount { get; set; }

        /// <summary>Yöneticisi atanmamış olduğu için 1. aşaması atlanan açık talep sayısı.</summary>
        public int YoneticisizCount { get; set; }

        /// <summary>Her talep için çalışanın onay sonrası kalacak günü.</summary>
        public Dictionary<int, int> KalanSonrasi { get; set; } = new();

        /// <summary>Geriye dönük uyumluluk — eski görünümler PendingCount okuyordu.</summary>
        public int PendingCount => AcikCount;
    }

    // ═══════════════════════════════════════════════════════════
    //  ONAY BAĞLAMI — İK/yönetici karar verirken gördüğü bilgiler
    // ═══════════════════════════════════════════════════════════

    public class OnayBaglamVm
    {
        public LeaveRequest? Talep { get; set; }

        public LeaveBalanceVm Bakiye { get; set; } = new();

        /// <summary>Çalışanın yöneticisi (şu anki tanım).</summary>
        public string? YoneticiAdi { get; set; }

        /// <summary>
        /// Aynı tarihlerde izinli ya da izin bekleyen diğer çalışanlar.
        /// Aynı departman veya aynı yöneticiye bağlı olanlar dikkate alınır —
        /// "aynı gün ekipte kimse kalmıyor mu" sorusunun cevabı.
        /// </summary>
        public List<CakismaSatirVm> Cakismalar { get; set; } = new();

        /// <summary>Çalışanın bu yıldaki diğer talepleri — kullanım alışkanlığı.</summary>
        public List<GecmisTalepVm> GecmisTalepler { get; set; } = new();

        /// <summary>Onaylanırsa kalacak gün. Haktan düşmeyen türlerde null.</summary>
        public int? OnaySonrasiKalan { get; set; }

        public bool BakiyeYetersiz => OnaySonrasiKalan.HasValue && OnaySonrasiKalan.Value < 0;

        public int CakismaSayisi => Cakismalar.Count;
    }

    public class CakismaSatirVm
    {
        public int RequestId { get; set; }
        public string FullName { get; set; } = "";
        public string? Title { get; set; }
        public string? Departman { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveTypeName { get; set; } = "";
        public string? StatusCode { get; set; }
        public string? StatusName { get; set; }

        /// <summary>Aynı yöneticiye bağlı mı (ekip arkadaşı) yoksa sadece aynı departman mı?</summary>
        public bool AyniEkip { get; set; }

        /// <summary>Kesişen gün sayısı.</summary>
        public int KesisenGun { get; set; }
    }

    public class GecmisTalepVm
    {
        public int RequestId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public string LeaveTypeName { get; set; } = "";
        public string? StatusCode { get; set; }
        public string? StatusName { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  EKİP ONAY EKRANI — yöneticinin kendi astları
    // ═══════════════════════════════════════════════════════════

    public class EkipIzinIndexVm
    {
        public int Year { get; set; } = DateTime.Now.Year;
        public List<int> AvailableYears { get; set; } = new();

        /// <summary>Kararımı bekleyenler — ekranın asıl işi.</summary>
        public List<EkipTalepSatirVm> Bekleyenler { get; set; } = new();

        /// <summary>Kararımı verdiğim ya da üst aşamada olan talepler.</summary>
        public List<EkipTalepSatirVm> Gecmis { get; set; } = new();

        /// <summary>Ekip üyeleri ve bakiyeleri.</summary>
        public List<LeaveBalanceVm> Ekip { get; set; } = new();

        public int BugunIzinliSayisi { get; set; }

        public bool EkibimVar => Ekip.Count > 0;
    }

    public class EkipTalepSatirVm
    {
        public int RequestId { get; set; }
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public string LeaveTypeName { get; set; } = "";
        public bool HaktanDuser { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? StatusCode { get; set; }
        public string? StatusName { get; set; }

        public bool? SupervisorOnayladi { get; set; }
        public string? SupervisorNote { get; set; }
        public DateTime? SupervisorDecisionAt { get; set; }

        /// <summary>İK nihai kararını verdi mi, ne dedi?</summary>
        public string? IkKarari { get; set; }

        /// <summary>Onaylanırsa çalışanın kalacak günü. Haktan düşmeyende null.</summary>
        public int? OnaySonrasiKalan { get; set; }

        /// <summary>Aynı tarihlerde izinli olan diğer ekip üyesi sayısı.</summary>
        public int EkipCakismasi { get; set; }

        public bool BenimKararimiBekliyor => StatusCode == "Pending";
    }

    // ═══════════════════════════════════════════════════════════
    //  İK — BAKİYE TABLOSU
    // ═══════════════════════════════════════════════════════════

    public class LeaveBalanceListVm
    {
        public int Year { get; set; } = DateTime.Now.Year;
        public List<int> AvailableYears { get; set; } = new();
        public List<LeaveBalanceVm> Balances { get; set; } = new();

        public int ToplamHakedis => Balances.Sum(x => x.Entitlement);
        public int ToplamKullanilan => Balances.Sum(x => x.UsedDays);
        public int ToplamBekleyen => Balances.Sum(x => x.PendingDays);
        public int ToplamKalan => Balances.Sum(x => x.RemainingDays);
        public int HakedisGirilmemisSayisi => Balances.Count(x => !x.HasRecord);
        public int IseGirisEksikSayisi => Balances.Count(x => x.IseGirisEksik);

        /// <summary>Hakedişi kanuni asgarinin altında kalanlar.</summary>
        public int AsgariAltindaSayisi => Balances.Count(x => x.AsgariAltinda);

        /// <summary>Bakiyesi eksiye düşmüş olanlar — hemen müdahale gerekir.</summary>
        public int EksiBakiyeSayisi => Balances.Count(x => x.RemainingDays < 0);

        /// <summary>Yıl hiç hazırlanmamış mı (kimsede kayıt yok)?</summary>
        public bool YilHazirlanmamis => Balances.Count > 0 && HakedisGirilmemisSayisi == Balances.Count;

        /// <summary>Ekranın en üstünde gösterilecek tek cümlelik durum.</summary>
        public string DurumCumlesi
        {
            get
            {
                if (Balances.Count == 0) return "İzin takibine dahil çalışan bulunamadı.";
                if (YilHazirlanmamis) return $"{Year} yılı henüz hazırlanmamış — hiçbir çalışanın hakedişi girilmemiş.";
                if (HakedisGirilmemisSayisi > 0) return $"{HakedisGirilmemisSayisi} çalışanın {Year} hakedişi eksik.";
                if (EksiBakiyeSayisi > 0) return $"{EksiBakiyeSayisi} çalışanın bakiyesi eksiye düşmüş.";
                if (AsgariAltindaSayisi > 0) return $"{AsgariAltindaSayisi} çalışanın hakedişi kanuni asgarinin altında.";
                return $"{Year} yılı hazır — {Balances.Count} çalışanın hakedişi tanımlı.";
            }
        }

        public string DurumSinifi
        {
            get
            {
                if (Balances.Count == 0 || YilHazirlanmamis) return "uyari";
                if (HakedisGirilmemisSayisi > 0 || EksiBakiyeSayisi > 0) return "uyari";
                if (AsgariAltindaSayisi > 0) return "dikkat";
                return "iyi";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  İK — PERSONEL ÖZLÜK
    // ═══════════════════════════════════════════════════════════

    public class PersonelListVm
    {
        public List<PersonelSatirVm> Personeller { get; set; } = new();
        public int EksikBilgiSayisi => Personeller.Count(p => p.IseGirisTarihi == null);
        public int AktifSayisi => Personeller.Count(p => p.Aktif);
        public int YoneticisizSayisi => Personeller.Count(p => p.Aktif && string.IsNullOrEmpty(p.YoneticiUserId));
    }

    public class PersonelSatirVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? UserName { get; set; }
        public string? Title { get; set; }
        public string? Departman { get; set; }
        public string? Email { get; set; }
        public string? TcKimlikNo { get; set; }
        public DateTime? IseGirisTarihi { get; set; }
        public DateTime? DogumTarihi { get; set; }
        public DateTime? IstenCikisTarihi { get; set; }
        public bool Aktif { get; set; }

        public int Kidem { get; set; }
        public int Yas { get; set; }

        /// <summary>İçinde bulunduğumuz yıl için kıdeme göre hakediş.</summary>
        public int BuYilHakedis { get; set; }

        // ── Organizasyon ──
        public string? YoneticiUserId { get; set; }
        public string? YoneticiAdi { get; set; }
        public int AstSayisi { get; set; }
    }

    public class PersonelEditVm
    {
        public string UserId { get; set; } = "";

        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; } = "";

        [Display(Name = "Kullanıcı Adı")]
        public string? UserName { get; set; }

        [Display(Name = "Unvan")]
        [MaxLength(150)]
        public string? Title { get; set; }

        [Display(Name = "Departman")]
        [MaxLength(150)]
        public string? Departman { get; set; }

        [Display(Name = "TC Kimlik No")]
        [MaxLength(11)]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "TC kimlik no 11 haneli olmalıdır.")]
        public string? TcKimlikNo { get; set; }

        [Display(Name = "İşe Giriş Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? IseGirisTarihi { get; set; }

        [Display(Name = "Doğum Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? DogumTarihi { get; set; }

        [Display(Name = "İşten Çıkış Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? IstenCikisTarihi { get; set; }

        // ── Bilgi amaçlı ──
        public int Kidem { get; set; }
        public int Yas { get; set; }
        public int BuYilHakedis { get; set; }
        public string? HakedisAciklamasi { get; set; }
        public List<LeaveBalanceVm> Bakiyeler { get; set; } = new();

        /// <summary>Mevcut yönetici — bilgi amaçlı; atama Organizasyon ekranından yapılır.</summary>
        public string? YoneticiAdi { get; set; }
        public int AstSayisi { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  ORGANİZASYON ŞEMASI
    // ═══════════════════════════════════════════════════════════

    public class OrganizasyonVm
    {
        /// <summary>Kök düğümler — yöneticisi olmayan (en üstteki) çalışanlar.</summary>
        public List<OrgDugumVm> Kokler { get; set; } = new();

        /// <summary>Atama açılırı için tüm aktif çalışanlar.</summary>
        public List<OrgKisiVm> TumCalisanlar { get; set; } = new();

        /// <summary>Yöneticisi atanmamış aktif çalışanlar — İK'nın yapacak işi.</summary>
        public List<OrgKisiVm> Yoneticisizler { get; set; } = new();

        public int AktifCalisanSayisi { get; set; }
        public int YoneticiSayisi { get; set; }
        public int EnDerinSeviye { get; set; }

        /// <summary>Şemada görünen kişi sayısı — kökten erişilemeyen kalırsa fark eder.</summary>
        public int SemadakiKisiSayisi => Kokler.Sum(k => k.AltAgacSayisi);
    }

    public class OrgDugumVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Title { get; set; }
        public string? Departman { get; set; }
        public bool Aktif { get; set; }
        public int Seviye { get; set; }

        public List<OrgDugumVm> Astlar { get; set; } = new();

        public int DogrudanAstSayisi => Astlar.Count;

        /// <summary>Kendisi dahil alt ağaçtaki toplam kişi.</summary>
        public int AltAgacSayisi => 1 + Astlar.Sum(a => a.AltAgacSayisi);

        /// <summary>Açık izin talebi olan ast sayısı — şemada rozet olarak gösterilir.</summary>
        public int BekleyenTalepSayisi { get; set; }

        public string BasHarfler
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName)) return "?";
                var p = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 1) return p[0].Substring(0, 1).ToUpperInvariant();
                return (p[0].Substring(0, 1) + p[^1].Substring(0, 1)).ToUpperInvariant();
            }
        }
    }

    public class OrgKisiVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Title { get; set; }
        public string? Departman { get; set; }
        public bool Aktif { get; set; }
        public string? YoneticiUserId { get; set; }
        public string? YoneticiAdi { get; set; }
        public int AstSayisi { get; set; }

        public string Etiket => string.IsNullOrWhiteSpace(Title)
            ? FullName
            : $"{FullName} — {Title}";
    }

    // ═══════════════════════════════════════════════════════════
    //  TAKVİM
    // ═══════════════════════════════════════════════════════════

    public class LeaveCalendarDayVm
    {
        public DateTime Tarih { get; set; }
        public bool AyinIcinde { get; set; }
        public bool Bugun { get; set; }
        public bool HaftaSonu { get; set; }
        public string? TatilAdi { get; set; }
        public bool TatilTamGun { get; set; }
        public List<LeaveCalendarEntryVm> Izinliler { get; set; } = new();
    }

    public class LeaveCalendarEntryVm
    {
        public int RequestId { get; set; }
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string LeaveTypeName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName)) return "?";
                var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
                return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
            }
        }

        /// <summary>Kullanıcı id'sinden türetilen sabit renk indeksi (0–7).</summary>
        public int RenkIndeksi
        {
            get
            {
                if (string.IsNullOrEmpty(UserId)) return 0;
                int h = 0;
                foreach (var c in UserId) h = (h * 31 + c) % 8;
                return Math.Abs(h);
            }
        }
    }

    public class LeaveCalendarVm
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public DateTime AyBasi => new DateTime(Yil, Ay, 1);
        public string AyAdi => AyBasi.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));

        /// <summary>Pazartesiyle başlayan 6 hafta × 7 gün.</summary>
        public List<LeaveCalendarDayVm> Gunler { get; set; } = new();

        public int IzinliCalisanSayisi { get; set; }
        public int ToplamIzinGunu { get; set; }

        /// <summary>Bu ay izinli olan çalışanların listesi — yan panel için.</summary>
        public List<LeaveCalendarEntryVm> AyinIzinleri { get; set; } = new();

        public DateTime OncekiAy => AyBasi.AddMonths(-1);
        public DateTime SonrakiAy => AyBasi.AddMonths(1);
    }

    // ═══════════════════════════════════════════════════════════
    //  RESMİ TATİL
    // ═══════════════════════════════════════════════════════════

    public class ResmiTatilListVm
    {
        public int Yil { get; set; } = DateTime.Now.Year;
        public List<int> AvailableYears { get; set; } = new();
        public List<ResmiTatil> Tatiller { get; set; } = new();

        public int TamGunSayisi => Tatiller.Count(x => x.TamGun);
        public int YarimGunSayisi => Tatiller.Count(x => !x.TamGun);
    }
}