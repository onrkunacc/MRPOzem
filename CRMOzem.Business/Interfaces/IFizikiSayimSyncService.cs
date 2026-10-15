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
        public DateTime BaslamaZamani { get; set; } = DateTime.Now;
        public DateTime BitisZamani { get; set; }
        public string? HataMesaji { get; set; }

        public string Ozet =>
            $"Çekilen: {CekilenSatir} · Yeni: {YeniEklenen} · Güncellenen: {Guncellenen}"
            + (AtlanilanHastaneSizKayit > 0 ? $" · Atlanan: {AtlanilanHastaneSizKayit}" : "")
            + $" · Reaktif eşleşen: {ReaktifEslesen}/{ReaktifEslesen + ReaktifEslesemeyen}"
            + $" · {(BitisZamani - BaslamaZamani).TotalSeconds:N1}sn";
    }

    public class EslesmeRaporuVm
    {
        public List<EslesmeDetay> ApidenGelenHastaneler { get; set; } = new();
        public List<string> CRMOzemdekiHastaneler { get; set; } = new();
        public int EslesenSayisi { get; set; }
        public int EslesemeyenSayisi { get; set; }
    }

    public class EslesmeDetay
    {
        public int ApiHospitalId { get; set; }
        public string ApiHospitalName { get; set; } = "";
        public int SatirSayisi { get; set; }
        public bool Eslesti { get; set; }
        public int? InternalHospitalId { get; set; }
        public string? InternalHospitalName { get; set; }
    }
}
