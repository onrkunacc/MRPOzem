using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ISiparisSyncService
    {
        /// <summary>
        /// Sipariş yazılımından bekleyen siparişleri çeker, yeni olanları
        /// talep olarak kaydeder ve önerilerini hesaplar. Yalnızca OKUR —
        /// sipariş yazılımına hiçbir şey yazmaz.
        /// </summary>
        Task<SiparisSyncSonuc> SyncAsync(CancellationToken ct = default);

        /// <summary>
        /// Bekleyen taleplerin kalemlerini katalogla YENİDEN eşleştirir.
        /// Eşleştirme kuralı ya da katalog (Tests) değiştiğinde, daha önce
        /// "eşleşmedi" kalan kalemler için çalıştırılır. Sipariş yazılımına
        /// ulaşılabiliyorsa miktar ve ambalaj bilgisi de oradan tazelenir.
        /// </summary>
        Task<YenidenEslestirSonuc> YenidenEslestirAsync(CancellationToken ct = default);

        /// <summary>
        /// Sipariş yazılımındaki bekleyen siparişleri bizdeki kayıtlarla
        /// karşılaştırır ve GELMEYEN her sipariş için nedenini söyler.
        /// Hiçbir şey kaydetmez; yalnızca okur.
        /// </summary>
        Task<SiparisTeshisSonuc> TeshisAsync(CancellationToken ct = default);
    }

    public class SiparisTeshisSonuc
    {
        public bool ApiyeUlasildi { get; set; }
        public bool ApiSinirinaTakildi { get; set; }
        public string? HataMesaji { get; set; }

        /// <summary>Sipariş yazılımında "bekliyor" durumundaki sipariş sayısı.</summary>
        public int ApidekiSiparis { get; set; }
        public int Alinmis { get; set; }

        /// <summary>Engeli olmayan, "Siparişleri Çek" denince gelecek sipariş.</summary>
        public int CekilmeyiBekleyen { get; set; }

        /// <summary>60 günden eski olduğu için bilerek alınmayan sipariş.</summary>
        public int EskiOlduguIcinAlinmayan { get; set; }

        public List<GelmeyenSiparis> Gelmeyenler { get; set; } = new();
        public List<EslesmeyenMusteri> EslesmeyenMusteriler { get; set; } = new();
    }

    public class GelmeyenSiparis
    {
        public int OrderId { get; set; }
        public string MusteriKodu { get; set; } = "";
        public string? KurumAdi { get; set; }
        public DateTime? Tarih { get; set; }
        public int KalemSayisi { get; set; }
        public string Neden { get; set; } = "";
        public string Cozum { get; set; } = "";
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

        /// <summary>60 günden eski olduğu için alınmayan sipariş.</summary>
        public int EskiAtlanan { get; set; }

        /// <summary>
        /// Tarihi okunamadığı için "bugün" kabul edilen sipariş.
        /// (Eskiden bunlar sessizce "çok eski" sayılıp atlanıyordu.)
        /// </summary>
        public int TarihOkunamayan { get; set; }

        public int YeniKalem { get; set; }
        public int EslesenKalem { get; set; }
        public int EslesmeyenKalem { get; set; }
        public int OtomatikOnerilenKalem { get; set; }

        /// <summary>Sipariş yazılımından gelen ambalaj türleri ve kalem sayıları.</summary>
        public Dictionary<string, int> AmbalajDagilimi { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sipariş yazılımı en yeni 500 siparişi döner. Dönen en eski sipariş
        /// hâlâ 60 günlük alım penceresindeyse daha eskileri görülememiş olabilir.
        /// </summary>
        public bool ApiSinirinaTakildi { get; set; }

        public List<EslesmeyenMusteri> EslesmeyenMusteriler { get; set; } = new();

        public DateTime BaslamaZamani { get; set; } = DateTime.Now;
        public DateTime BitisZamani { get; set; }
        public string? HataMesaji { get; set; }

        public string Ozet
        {
            get
            {
                var p = new List<string>
                {
                    $"Gelen: {ApidanGelenToplam}",
                    $"Yeni: {YeniKaydedilen}",
                    $"Zaten vardı: {MevcutAtlanan}"
                };

                if (YeniKalem > 0) p.Add($"Kalem: {EslesenKalem}/{YeniKalem} eşleşti");
                if (EslesmeyokAtlanan > 0) p.Add($"Hastane eşleşmesi yok: {EslesmeyokAtlanan}");
                if (YoksayAtlanan > 0) p.Add($"Yoksayılan: {YoksayAtlanan}");
                if (EskiAtlanan > 0) p.Add($"60 günden eski: {EskiAtlanan}");
                if (KalemiOlmayanAtlanan > 0) p.Add($"Kalemsiz: {KalemiOlmayanAtlanan}");
                if (TarihOkunamayan > 0) p.Add($"Tarihi okunamayan: {TarihOkunamayan}");
                if (ApiSinirinaTakildi) p.Add("⚠ son 60 günde 500'den fazla sipariş var, tamamı okunamadı");

                p.Add($"{(BitisZamani - BaslamaZamani).TotalSeconds:N1} sn");
                return string.Join(" · ", p);
            }
        }

        public string AmbalajOzeti => AmbalajDagilimi.Count == 0
            ? ""
            : string.Join(", ", AmbalajDagilimi.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value}"));
    }

    public class EslesmeyenMusteri
    {
        public string MusteriKodu { get; set; } = "";
        public string? KurumAdi { get; set; }
        public string? MusteriAdi { get; set; }
        public int SiparisSayisi { get; set; }
        public int SonOrderId { get; set; }
    }

    public class YenidenEslestirSonuc
    {
        public bool Basarili { get; set; }
        public int IncelenenKalem { get; set; }

        /// <summary>Daha önce eşleşmeyip şimdi eşleşen kalem.</summary>
        public int YeniEslesen { get; set; }

        /// <summary>Eşleştiği ürün ya da miktarı değişen kalem.</summary>
        public int Degisen { get; set; }

        public int HalaEslesmeyen { get; set; }

        /// <summary>Miktarı sipariş yazılımından tazelenebilen kalem.</summary>
        public int ApidenTazelenen { get; set; }

        public bool ApiyeUlasildi { get; set; }
        public string? HataMesaji { get; set; }

        public string Ozet =>
            $"{IncelenenKalem} kalem incelendi · {YeniEslesen} yeni eşleşti · {Degisen} güncellendi · "
            + $"{HalaEslesmeyen} hâlâ eşleşmiyor"
            + (ApiyeUlasildi ? "" : " · sipariş yazılımına ulaşılamadı, miktarlar tazelenmedi");
    }
}
