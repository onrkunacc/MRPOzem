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
        /// Bakiyeye dokunmaz — düşüm onay anında olur.
        /// </summary>
        Task<(bool Basarili, string? Hata)> CreateLeaveAsync(LeaveRequest request);

        Task<(bool Basarili, string? Hata)> CancelLeaveAsync(int leaveRequestId, string userId);

        Task<bool> IsOwnerAsync(int leaveRequestId, string userId);

        // ═══════════ İK — TALEPLER ═══════════

        Task<List<LeaveRequest>> GetAllAsync(int? yil = null, string? statusCode = null);

        Task<LeaveRequest?> GetByIdAsync(int id);

        Task<(bool Basarili, string? Hata)> ApproveLeaveAsync(int leaveRequestId, string approverUserId, string? not = null);

        Task<(bool Basarili, string? Hata)> RejectLeaveAsync(int leaveRequestId, string approverUserId, string gerekce);

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
