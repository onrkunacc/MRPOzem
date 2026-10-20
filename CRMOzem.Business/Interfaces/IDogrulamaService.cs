using CRMOzem.Core.ViewModels.Dogrulama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IDogrulamaService
    {
        Task<DogrulamaIndexVm> GetListAsync(DogrulamaFiltreVm filtre, CancellationToken ct);

        Task<DogrulamaDetayVm?> GetDetayAsync(int talepId, CancellationToken ct);

        /// <summary>Hastane isteğini aynen onayla.</summary>
        Task<(bool ok, string msg)> OnaylaAynenAsync(
            int talepId, string? onayNotu, string? kullanici, CancellationToken ct);

        /// <summary>Operatörün düzelttiği miktarlarla onayla.</summary>
        Task<(bool ok, string msg)> OnaylaDuzeltilmisAsync(
            OnayPostDto dto, string? kullanici, CancellationToken ct);

        Task<(bool ok, string msg)> ReddetAsync(
            int talepId, string? redNedeni, string? kullanici, CancellationToken ct);

        /// <summary>Birden çok talebi aynen onayla (toplu).</summary>
        Task<int> TopluOnaylaAynenAsync(
            IEnumerable<int> talepIds, string? kullanici, CancellationToken ct);
    }
}
