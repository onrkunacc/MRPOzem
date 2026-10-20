using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ISiparisSyncService
    {
        Task<SiparisSyncSonuc> SyncAsync(CancellationToken ct = default);
    }

    public class SiparisSyncSonuc
    {
        public bool Basarili { get; set; }
        public int ApidanGelenToplam { get; set; }
        public int YeniKaydedilen { get; set; }
        public int MevcutAtlanan { get; set; }
        public int EslesmeyokAtlanan { get; set; }
        public int YoksayAtlanan { get; set; }
        public int KalemiOlmayanAtlanan { get; set; }
        public int OtomatikOnerilenKalem { get; set; }
        public List<EslesmeyenMusteri> EslesmeyenMusteriler { get; set; } = new();
        public DateTime BaslamaZamani { get; set; } = DateTime.Now;
        public DateTime BitisZamani { get; set; }
        public string? HataMesaji { get; set; }

        public string Ozet =>
            $"API: {ApidanGelenToplam} · Yeni: {YeniKaydedilen} · Zaten vardı: {MevcutAtlanan}"
            + (EslesmeyokAtlanan > 0 ? $" · Eşleşme yok: {EslesmeyokAtlanan}" : "")
            + (YoksayAtlanan > 0 ? $" · Yoksayılan: {YoksayAtlanan}" : "")
            + $" · Öneri: {OtomatikOnerilenKalem}"
            + $" · {(BitisZamani - BaslamaZamani).TotalSeconds:N1}sn";
    }

    public class EslesmeyenMusteri
    {
        public string MusteriKodu { get; set; } = "";
        public string? KurumAdi { get; set; }
        public string? MusteriAdi { get; set; }
        public int SiparisSayisi { get; set; }
        public int SonOrderId { get; set; }
    }
}
