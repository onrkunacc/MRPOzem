using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;

namespace CRMOzem.Business.Interfaces
{
    public interface ILeaveService
    {
        // ═══════════ ÇALIŞAN ═══════════

        Task<List<LeaveRequest>> GetUserLeavesAsync(string userId, int? yil = null);

        Task<LeaveBalanceVm> GetBalanceAsync(string userId, int yil);

        /// <summary>Geriye dönük uyumluluk — sadece kalan gün.</summary>
        Task<int> GetRemainingLeaveDaysAsync(string userId);

        /// <summary>
        /// Yeni izin talebi. Gün sayısını hesaplar, çakışma ve bakiye kontrolü yapar.
        /// Bakiyeye dokunmaz — düşüm İK onayı anında olur.
        ///
        /// Çalışanın yöneticisi tanımlıysa talep 1. aşamada (Pending) açılır;
        /// tanımlı değilse 1. aşama atlanıp doğrudan İK onayına (SupervisorApproved) düşer.
        /// </summary>
        Task<(bool Basarili, string? Hata)> CreateLeaveAsync(LeaveRequest request);

        Task<(bool Basarili, string? Hata)> CancelLeaveAsync(int leaveRequestId, string userId);

        Task<bool> IsOwnerAsync(int leaveRequestId, string userId);

        // ═══════════ AŞAMA 1 — YÖNETİCİ ONAYI ═══════════

        /// <summary>Bu kullanıcının doğrudan astlarının id'leri.</summary>
        Task<List<string>> GetAstIdleriAsync(string supervisorUserId);

        /// <summary>Bu kullanıcı birilerinin yöneticisi mi? Menüde "Ekip İzinleri" göstermek için.</summary>
        Task<bool> YoneticiMiAsync(string userId);

        /// <summary>Bu kullanıcının kendi kararını bekleyen ast talebi sayısı.</summary>
        Task<int> GetEkipBekleyenSayisiAsync(string supervisorUserId);

        /// <summary>Çalışanın şu anki yöneticisinin adı — yoksa null.</summary>
        Task<string?> GetYoneticiAdiAsync(string userId);

        /// <summary>Yöneticinin ekip onay ekranı.</summary>
        Task<EkipIzinIndexVm> GetEkipTalepleriAsync(string supervisorUserId, int yil);

        /// <summary>
        /// Yönetici 1. aşamayı onaylar — talep İK onayına (SupervisorApproved) geçer.
        /// Bakiyeye dokunmaz. Çağıranın gerçekten o çalışanın yöneticisi olduğu doğrulanır.
        /// </summary>
        Task<(bool Basarili, string? Hata)> SupervisorApproveAsync(
            int leaveRequestId, string supervisorUserId, string? not = null);

        /// <summary>
        /// Yönetici 1. aşamada reddeder — talep doğrudan kapanır (Rejected).
        /// İK'ya düşmez; gerekçe çalışana görünür.
        /// </summary>
        Task<(bool Basarili, string? Hata)> SupervisorRejectAsync(
            int leaveRequestId, string supervisorUserId, string gerekce);

        // ═══════════ AŞAMA 2 — İK (NİHAİ) ═══════════

        Task<List<LeaveRequest>> GetAllAsync(int? yil = null, string? statusCode = null);

        Task<LeaveRequest?> GetByIdAsync(int id);

        /// <summary>
        /// İK nihai onayı. Hem İK onayında (SupervisorApproved) hem de henüz
        /// yönetici onayında (Pending) olan talep onaylanabilir — İK son söz sahibi.
        /// Pending bir talep İK tarafından onaylanırsa 1. aşamanın atlandığı kayda geçer.
        /// </summary>
        Task<(bool Basarili, string? Hata)> ApproveLeaveAsync(
            int leaveRequestId, string approverUserId, string? not = null);

        Task<(bool Basarili, string? Hata)> RejectLeaveAsync(
            int leaveRequestId, string approverUserId, string gerekce);

        /// <summary>
        /// Karar verirken gereken bağlam: bakiye, onay sonrası kalan,
        /// aynı tarihlerde izinli ekip arkadaşları ve çalışanın yıl içindeki
        /// diğer talepleri.
        /// </summary>
        Task<OnayBaglamVm?> GetOnayBaglamiAsync(int leaveRequestId);

        // ═══════════ İK — BAKİYE ═══════════

        Task<List<LeaveBalanceVm>> GetAllBalancesAsync(int yil);

        /// <summary>
        /// İK elle hakediş girer. ManuelGirildi = true olur, otomatik hesaplama bu kaydı ezmez.
        /// </summary>
        Task<(bool Basarili, string? Hata)> SetEntitlementAsync(
            string userId, int yil, int hakedis, int devreden, string? aciklama = null);

        /// <summary>
        /// Tek çalışanın hakedişini kıdeminden hesaplar ve kaydeder.
        /// ManuelGirildi = false olur.
        /// </summary>
        Task<(bool Basarili, string? Hata, int Hakedis)> OtomatikHakedisAsync(
            string userId, int yil, bool devriAktar);

        /// <summary>
        /// Tüm aktif çalışanlar için yılı toplu oluşturur.
        /// Elle girilmiş kayıtlara dokunmaz (<paramref name="manuelleriEz"/> false ise).
        /// </summary>
        Task<(int Olusturulan, int Guncellenen, int Atlanan)> YilToplamOlusturAsync(
            int yil, bool devriAktar, bool manuelleriEz);

        // ═══════════ İK — PERSONEL ÖZLÜK ═══════════

        Task<PersonelListVm> GetPersonelListAsync(bool sadeceAktif = false);

        Task<PersonelEditVm?> GetPersonelAsync(string userId);

        Task<(bool Basarili, string? Hata)> UpdatePersonelAsync(PersonelEditVm model);

        // ═══════════ ORGANİZASYON ŞEMASI ═══════════

        /// <summary>Şema ağacı + yöneticisi atanmamışlar + atama açılırı.</summary>
        Task<OrganizasyonVm> GetOrganizasyonAsync();

        /// <summary>
        /// Çalışanın yöneticisini atar ya da kaldırır (<paramref name="supervisorUserId"/> null).
        /// Döngü oluşturacak atamayı reddeder — kimse kendi astının astı olamaz.
        /// </summary>
        Task<(bool Basarili, string? Hata)> SetSupervisorAsync(
            string userId, string? supervisorUserId, string atayanUserId);

        /// <summary>
        /// Bir yöneticinin tüm astlarını başka bir yöneticiye devreder —
        /// işten çıkış / görev değişikliği durumunda tek tek uğraşmamak için.
        /// </summary>
        Task<(bool Basarili, string? Hata, int Tasinan)> AstlariDevretAsync(
            string eskiSupervisorUserId, string? yeniSupervisorUserId, string atayanUserId);

        // ═══════════ TAKVİM ═══════════

        Task<List<LeaveRequest>> GetApprovedInRangeAsync(DateTime baslangic, DateTime bitis);

        Task<LeaveCalendarVm> GetCalendarAsync(int yil, int ay);

        // ═══════════ ORTAK ═══════════

        Task<List<LeaveType>> GetLeaveTypesAsync();

        /// <summary>
        /// İki tarih arasındaki iş günü. Pazar her zaman hariç;
        /// cumartesi yalnız <paramref name="cumartesiHaric"/> true ise hariç.
        /// Tam gün resmi tatiller düşülür.
        /// </summary>
        Task<int> HesaplaIsGunuAsync(DateTime baslangic, DateTime bitis, bool cumartesiHaric);

        /// <summary>Bakiyeyi onaylı izinlerden yeniden hesaplar — kendi kendini onarır.</summary>
        Task RecalculateBalanceAsync(string userId, int yil);

        Task<List<int>> GetAvailableYearsAsync();
    }
}
