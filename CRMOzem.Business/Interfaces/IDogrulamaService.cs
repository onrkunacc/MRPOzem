using CRMOzem.Core.ViewModels.Dogrulama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IDogrulamaService
    {
        Task<DogrulamaIndexVm> GetListAsync(DogrulamaFiltreVm filtre, CancellationToken ct);

        Task<DogrulamaDetayVm?> GetDetayAsync(int talepId, CancellationToken ct);

        /// <summary>
        /// Bekleyen taleplerin önerisini ve hükmünü GÜNCEL veriyle yeniden hesaplar
        /// ve kalemlere yazar. Yeni sayım ya da istatistik girildiğinde eski
        /// öneri ekranda kalmasın diye liste ve detay açılışında çağrılır.
        /// Onaylanmış/reddedilmiş taleplere dokunmaz — onlar karar anındaki
        /// rakamlarıyla saklanır.
        /// </summary>
        /// <param name="talepIds">Boşsa tüm bekleyen talepler.</param>
        /// <returns>Güncellenen kalem sayısı.</returns>
        Task<int> YenidenHesaplaAsync(IEnumerable<int>? talepIds, CancellationToken ct);

        /// <summary>Hastane isteğini aynen onayla.</summary>
        Task<(bool ok, string msg)> OnaylaAynenAsync(
            int talepId, string? onayNotu, string? kullanici, CancellationToken ct);

        /// <summary>
        /// Ekrandaki miktarlarla onayla. Hiçbir miktar değişmediyse "aynen",
        /// değiştiyse "düzeltildi" olarak kaydedilir — ayrımı sistem yapar.
        /// </summary>
        Task<(bool ok, string msg)> OnaylaDuzeltilmisAsync(
            OnayPostDto dto, string? kullanici, CancellationToken ct);

        Task<(bool ok, string msg)> ReddetAsync(
            int talepId, string? redNedeni, string? kullanici, CancellationToken ct);

        /// <summary>Birden çok talebi aynen onayla (toplu).</summary>
        /// <summary>
        /// Belirtilen günden eski BEKLEYEN talepleri "Gönderildi" durumuna alıp
        /// doğrulama kuyruğundan çıkarır. Sipariş yazılımında siparişler
        /// işlendikten sonra da "bekliyor" kaldığı için, doğrulama devreye
        /// alınmadan önce zaten gönderilmiş siparişler kuyrukta birikir;
        /// bunlar hem listeyi şişirir hem de sonraki siparişlerin "önceki
        /// bekleyen talep var" diye fazla görünmesine yol açar.
        /// Kalem miktarlarına dokunmaz; yalnızca talebin durumunu değiştirir.
        /// </summary>
        Task<int> EskiBekleyenleriKapatAsync(int gunEski, string? kullanici, CancellationToken ct);

        Task<int> TopluOnaylaAynenAsync(
    IEnumerable<int> talepIds, string? kullanici, CancellationToken ct);
    }
}
