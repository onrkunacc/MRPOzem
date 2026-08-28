using CRMOzem.Core.ViewModels.KotaTakip;

namespace CRMOzem.Business.Interfaces
{
    public interface IKotaTakipService
    {
        Task<KotaTakipIndexViewModel> GetIndexAsync(int yil, int? ay, int? duzenlenecekKayitId = null);
        Task<KotaOzetViewModel> GetOzetAsync(int yil, int? ay);
        Task KayitKaydetAsync(KotaFinansKaydiInputViewModel model, string? kullaniciId);
        Task KayitSilAsync(int id);
        Task KotaEkleAsync(KotaInputViewModel model);
        Task KotaCariEkleAsync(KotaCariInputViewModel model);
        Task KotaCariSilAsync(int id);
        Task<KotaSenkronizasyonSonucu> GelirleriSenkronizeEtAsync(int yil, int? ay, string? kullaniciId);
    }
}
