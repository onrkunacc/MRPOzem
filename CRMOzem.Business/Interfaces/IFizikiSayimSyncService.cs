using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{

    public interface IFizikiSayimSyncService
    {
        Task<FizikiSayimSyncSonuc> SyncAsync(int? hospitalId = null, CancellationToken ct = default);
        Task<EslesmeRaporuVm> TestEslesmeAsync(CancellationToken ct = default);
    }

    public class FizikiSayimSyncSonuc
    {
        public bool Basarili { get; set; }
        public int CekilenSatir { get; set; }
        public int YeniEklenen { get; set; }
        public int Guncellenen { get; set; }
        public int AtlanilanHastaneSizKayit { get; set; }
        public int ReaktifEslesen { get; set; }           // ← YENİ: kaç reaktif Test ile eşleşti
        public int ReaktifEslesemeyen { get; set; }       // ← YENİ: kaç reaktif eşleşmedi
        public List<string> EslesemeyenHastaneler { get; set; } = new();

        /// <summary>Yerine yeni sayım geldiği için pasife alınan eski sayım satırı.</summary>
        public int KapatilanEskiSatir { get; set; }
        public DateTime BaslamaZamani { get; set; } = DateTime.Now;
        public DateTime BitisZamani { get; set; }
        public string? HataMesaji { get; set; }

        public string Ozet =>
            $"Çekilen: {CekilenSatir} · Yeni: {YeniEklenen} · Güncellenen: {Guncellenen}"
            + (AtlanilanHastaneSizKayit > 0 ? $" · Hastanesi eşleşmediği için atlanan: {AtlanilanHastaneSizKayit}" : "")
            + (KapatilanEskiSatir > 0 ? $" · Kapatılan eski sayım satırı: {KapatilanEskiSatir}" : "")
            + $" · Reaktif eşleşen: {ReaktifEslesen}/{ReaktifEslesen + ReaktifEslesemeyen}"
            + $" · {(BitisZamani - BaslamaZamani).TotalSeconds:N1}sn";
    }

    public class EslesmeRaporuVm
    {
        public List<EslesmeDetay> ApidenGelenHastaneler { get; set; } = new();
        public List<string> CRMOzemdekiHastaneler { get; set; } = new();
        public int EslesenSayisi { get; set; }
        public int EslesemeyenSayisi { get; set; }

        /// <summary>false ise sayım uygulamasına ulaşılamadı.</summary>
        public bool ApiyeUlasildi { get; set; }
    }

    public class EslesmeDetay
    {
        public int ApiHospitalId { get; set; }
        public string ApiHospitalName { get; set; } = "";
        public int SatirSayisi { get; set; }
        public bool Eslesti { get; set; }
        public int? InternalHospitalId { get; set; }
        public string? InternalHospitalName { get; set; }
        public bool ManuelEslestirildi { get; set; }   // true = user manuel bağladı
        public bool ManuelYoksay { get; set; }         // true = user "yoksay" dedi

        /// <summary>Sayım uygulamasındaki son sayım tarihi.</summary>
        public DateTime? ApiSonSayim { get; set; }

        /// <summary>Bizde kayıtlı son (aktif) sayım tarihi.</summary>
        public DateTime? BizdekiSonSayim { get; set; }

        /// <summary>Sayım uygulamasında daha yeni sayım var ama bize düşmemiş.</summary>
        public bool BizdeEski => Eslesti && ApiSonSayim.HasValue
            && (!BizdekiSonSayim.HasValue || BizdekiSonSayim.Value.Date < ApiSonSayim.Value.Date);
    }
}
