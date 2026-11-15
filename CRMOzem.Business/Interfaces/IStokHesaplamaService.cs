using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    /// <summary>
    /// Stok, tüketim ve sipariş önerisinin TEK hesap yeri.
    /// Stok Öneri ekranı, sipariş doğrulama ve dashboard aynı motoru kullanır —
    /// iki ekranda iki farklı rakam çıkmasın diye.
    /// </summary>
    public interface IStokHesaplamaService
    {
        /// <summary>Geçerli hesap kuralları (hedef ay, tedarik süresi…).</summary>
        LojistikAyar Ayar { get; }

        /// <summary>Bir hastanenin tüm ürünleri için stok paneli.</summary>
        Task<HastaneStokPaneliVm?> HesaplaHastaneAsync(
            int hospitalId,
            int hedefRunwayAy = 3,
            CancellationToken ct = default);

        /// <summary>Tek ürün (hastane × test).</summary>
        Task<StokHesapSatirVm?> HesaplaUrunAsync(
            int hospitalId,
            int testId,
            int hedefRunwayAy = 3,
            CancellationToken ct = default);

        /// <summary>
        /// Birden çok hastaneyi tek seferde hesaplar — hastane sayısından
        /// bağımsız sabit sayıda sorgu atar.
        /// <paramref name="ekUrunler"/>: panelde normalde yer almayacak ama
        /// siparişte geçtiği için hesaplanması gereken (hastane, test) çiftleri.
        /// </summary>
        Task<Dictionary<int, HastaneStokPaneliVm>> HesaplaTopluAsync(
            IReadOnlyCollection<int> hospitalIds,
            int hedefAy = 3,
            IEnumerable<(int HospitalID, int TestID)>? ekUrunler = null,
            CancellationToken ct = default);

        /// <summary>Tüm aktif hastanelerin özet tablosu.</summary>
        Task<StokOzetSayfaVm> OzetAsync(int hedefAy = 3, CancellationToken ct = default);

        /// <summary>
        /// Bir sipariş kalemini hesaplanan ihtiyaçla karşılaştırıp hüküm verir.
        /// Veritabanına gitmez — saf hesap.
        /// </summary>
        /// <param name="satir">Ürünün stok satırı; eşleşmediyse null.</param>
        /// <param name="talepTest">Hastanenin istediği miktar (test).</param>
        /// <param name="oncekiBekleyenTest">
        /// Aynı ürün için bu talepten ÖNCE gelmiş ve hâlâ bekleyen taleplerin toplamı.
        /// İki talep ayrı ayrı "uygun" görünüp birlikte fazla çıkmasın diye.
        /// </param>
        SiparisKararVm Degerlendir(
            StokHesapSatirVm? satir,
            int talepTest,
            int oncekiBekleyenTest = 0,
            int? hedefAy = null);
    }
}
