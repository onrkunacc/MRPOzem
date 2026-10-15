using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ITcmbKurService
    {
        /// <summary>
        /// Belirli bir tarih için USD/TL kurunu getirir.
        /// Önce cache'den bakar, yoksa TCMB'den çeker ve cache'e yazar.
        /// Hafta sonu/tatil ise en yakın önceki iş gününü döner.
        /// </summary>
        Task<TcmbKur?> GetKurAsync(DateTime tarih, CancellationToken ct = default);

        /// <summary>
        /// Bugünün kurunu getirir (bugün yoksa en son iş gününü).
        /// </summary>
        Task<TcmbKur?> GetGuncelKurAsync(CancellationToken ct = default);

        /// <summary>
        /// USD tutarı TL'ye çevirir. Kur bulunamazsa null döner.
        /// </summary>
        Task<(decimal? tlKarsiligi, decimal? kur, DateTime? kurTarihi)> UsdToTryAsync(
            decimal usdTutar, DateTime tarih, CancellationToken ct = default);

        /// <summary>
        /// TL tutarı USD'ye çevirir. Kur bulunamazsa null döner.
        /// </summary>
        Task<(decimal? usdKarsiligi, decimal? kur, DateTime? kurTarihi)> TryToUsdAsync(
            decimal tlTutar, DateTime tarih, CancellationToken ct = default);

        /// <summary>
        /// Belirli bir tarih aralığındaki tüm kurları listeler.
        /// </summary>
        Task<List<TcmbKur>> GetKurListesiAsync(DateTime baslangic, DateTime bitis, CancellationToken ct = default);

        /// <summary>
        /// Manuel kur girişi (TCMB'de eksik olan bir günü kullanıcı manuel doldurur).
        /// </summary>
        Task<TcmbKur> ManuelKurEkleAsync(DateTime tarih, decimal usdAlis, decimal usdSatis, string? not, CancellationToken ct = default);

        /// <summary>
        /// TCMB'den belirli bir aralıktaki eksik günleri toplu çeker.
        /// </summary>
        Task<int> BulkFetchAsync(DateTime baslangic, DateTime bitis, CancellationToken ct = default);
    }
}
