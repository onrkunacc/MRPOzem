using CRMOzem.Core.DTOs;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IBirimFiyatExcelOkuyucu
    {
        
        ExcelOnizlemeDto Onizle(Stream dosya, string dosyaAdi, string? sayfaAdi = null, int baslikSatiri = 1);

        
        ExcelOkumaSonucu Oku(
            Stream dosya,
            KolonEslesmeDto eslesme,
            KarsilastirmaTarafi taraf,
            string paraBirimi,
            out List<BirimFiyatSatir> satirlar);
    }

   
    public interface IKurSaglayici
    {
        Task<KurSonucu> GetUsdTryAsync(DateTime tarih, CancellationToken ct = default);

        
        Task<Dictionary<DateTime, KurSonucu>> GetUsdTryAsync(IEnumerable<DateTime> tarihler, CancellationToken ct = default);
    }

 
    public interface IBirimFiyatService
    {
        
        Task<List<BirimFiyatKlasor>> GetKlasorAgaciAsync(CancellationToken ct = default);
        Task<BirimFiyatKlasor?> GetKlasorAsync(int id, CancellationToken ct = default);
        Task<(bool Basarili, string? Hata, int Id)> KlasorKaydetAsync(BirimFiyatKlasor klasor, CancellationToken ct = default);
        Task<(bool Basarili, string? Hata)> KlasorSilAsync(int id, CancellationToken ct = default);

      
        Task<List<BirimFiyatKarsilastirma>> GetKarsilastirmalarAsync(int? klasorId, string? arama = null, CancellationToken ct = default);
        Task<BirimFiyatKarsilastirma?> GetKarsilastirmaAsync(int id, CancellationToken ct = default);
        Task<(bool Basarili, string? Hata, int Id)> KarsilastirmaKaydetAsync(BirimFiyatKarsilastirma k, CancellationToken ct = default);
        Task<(bool Basarili, string? Hata)> KarsilastirmaSilAsync(int id, CancellationToken ct = default);
        Task<(bool Basarili, string? Hata)> KarsilastirmaTasiAsync(int id, int? hedefKlasorId, CancellationToken ct = default);

   
        Task<(bool Basarili, string? Hata)> SatirlariKaydetAsync(
            int karsilastirmaId,
            KarsilastirmaTarafi taraf,
            string dosyaAdi,
            List<BirimFiyatSatir> satirlar,
            CancellationToken ct = default);

        Task<List<BirimFiyatSatir>> GetSatirlarAsync(int karsilastirmaId, KarsilastirmaTarafi? taraf = null, CancellationToken ct = default);

        
        Task<HesaplamaSonucu> HesaplaAsync(int karsilastirmaId, CancellationToken ct = default);

        Task<List<BirimFiyatSonuc>> GetSonuclarAsync(
            int karsilastirmaId,
            SonucDurumu? durum = null,
            bool sadeceDikkat = false,
            string? arama = null,
            CancellationToken ct = default);
    }
}
