using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ISiparisSyncService
    {
        /// <summary>PHP'den bekleyen siparişleri çek, SiparisTalep olarak kaydet.</summary>
        Task<SiparisSyncSonuc> SyncAsync(CancellationToken ct = default);
    }

    public class SiparisSyncSonuc
    {
        public bool Basarili { get; set; }
        public int ApidanGelenToplam { get; set; }
        public int YeniKaydedilen { get; set; }
        public int MevcutAtlanan { get; set; }                   // zaten var olan (idempotency)
        public int HastaneSizAtlanan { get; set; }               // hastane eşleşmedi
        public int KalemiOlmayanAtlanan { get; set; }
        public int OtomatikOnerilenKalem { get; set; }           // StokHesaplama ile öneri üretilen
        public List<string> EslesemeyenHastaneler { get; set; } = new();
        public DateTime BaslamaZamani { get; set; } = DateTime.Now;
        public DateTime BitisZamani { get; set; }
        public string? HataMesaji { get; set; }

        public string Ozet =>
            $"API: {ApidanGelenToplam} · Yeni: {YeniKaydedilen} · Zaten vardı: {MevcutAtlanan}"
            + (HastaneSizAtlanan > 0 ? $" · Hastane yok: {HastaneSizAtlanan}" : "")
            + $" · Öneri üretilen kalem: {OtomatikOnerilenKalem}"
            + $" · {(BitisZamani - BaslamaZamani).TotalSeconds:N1}sn";
    }
}
