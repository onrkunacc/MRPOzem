using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    // ═══════════════════════════════════════════════════════════════════
    //  SİPARİŞ SENKRONU — sipariş yazılımı → CRMOzem (yalnızca okuma)
    // ═══════════════════════════════════════════════════════════════════
    //
    //  v2'de değişenler:
    //
    //   EŞLEŞTİRME
    //    · Referans numarası karşılaştırması harf/rakam dışını atıyor
    //      ("07P60-30" = "7P6030"). Eskiden tire/nokta farkı eşleşmeyi bozuyordu.
    //    · Sıra: hastanenin sayımındaki ürün → katalog referansı → birebir ad.
    //    · Eşleşmeyen kalemler sayılıyor ve ekranda listeleniyor.
    //
    //   MİKTAR
    //    · Ambalaj türü dikkate alınıyor (kutu / flakon / test). Eskiden her
    //      miktar "kutu" sayılıp kutu içi adetle çarpılıyordu.
    //
    //   SESSİZ KAYIPLAR
    //    · Tarihi okunamayan sipariş "çok eski" sanılıp atlanıyordu; artık
    //      bugün kabul edilip alınıyor ve sayılıyor.
    //    · 60 günden eski olduğu için alınmayanlar özet satırında görünüyor.
    //
    //   HIZ
    //    · Her kalem için hastanenin tüm ürünleri baştan hesaplanıyordu
    //      (40 kalemlik sipariş = 40 tam hesap). Öneri artık tüm bekleyenler
    //      için tek seferde, senkronun sonunda hesaplanıyor.
    // ═══════════════════════════════════════════════════════════════════
    public class SiparisSyncService : ISiparisSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly ISiparisApiClient _api;
        private readonly IDogrulamaService _dogrulama;
        private readonly ILogger<SiparisSyncService> _log;

        private const int ApiLimit = 500;
        private const int EskiSiparisGun = 60;

        public SiparisSyncService(
            ApplicationDbContext db,
            ISiparisApiClient api,
            IDogrulamaService dogrulama,
            ILogger<SiparisSyncService> log)
        {
            _db = db;
            _api = api;
            _dogrulama = dogrulama;
            _log = log;
        }

        // ═══════════════════════════════════════════════════════════
        //  SENKRON
        // ═══════════════════════════════════════════════════════════
        public async Task<SiparisSyncSonuc> SyncAsync(CancellationToken ct = default)
        {
            var sonuc = new SiparisSyncSonuc();
            var eslesmeyenToplam = new Dictionary<string, EslesmeyenMusteri>(StringComparer.OrdinalIgnoreCase);
            var yeniTalepIdler = new List<int>();

            try
            {
                var mevcutExternalRefler = await _db.SiparisTalepler
                    .AsNoTracking()
                    .Where(s => s.ExternalRefNo != null)
                    .Select(s => s.ExternalRefNo!)
                    .ToListAsync(ct);

                _log.LogInformation("Sipariş sync başlıyor. KayıtlıRefSayısı={N}", mevcutExternalRefler.Count);

                var (cevap, sinirdaKaldi) = await TumSiparisleriCekAsync(ct);
                if (cevap == null)
                {
                    sonuc.HataMesaji = "Sipariş yazılımı cevap vermedi. Adres ve API anahtarını "
                        + "(appsettings → SiparisApi) kontrol edin; ayrıntı uygulama log'unda.";
                    sonuc.BitisZamani = DateTime.Now;
                    return sonuc;
                }

                sonuc.ApidanGelenToplam = cevap.Orders.Count;
                sonuc.ApiSinirinaTakildi = sinirdaKaldi;

                if (cevap.Orders.Count == 0)
                {
                    sonuc.Basarili = true;
                    sonuc.BitisZamani = DateTime.Now;
                    return sonuc;
                }

                // ── Hastane eşleştirme tablosu ──
                // Eşleştirme kurum bazında: "müşteri kodu / kullanıcı no".
                // Müşteri kodu birçok kurumda ortak olabilir (bkz. SiparisMusteriCozucu).
                var tumEslesmeler = await _db.HospitalEslesmeleri.AsNoTracking().ToListAsync(ct);
                var cozucu = new SiparisMusteriCozucu(tumEslesmeler, cevap.Orders);

                var mevcutRefSet = new HashSet<string>(
                    mevcutExternalRefler.Where(x => !string.IsNullOrEmpty(x)),
                    StringComparer.OrdinalIgnoreCase);

                var ilgiliHastaneler = tumEslesmeler
                    .Where(e => !e.Yoksay && e.InternalHospitalID.HasValue)
                    .Select(e => e.InternalHospitalID!.Value)
                    .Distinct()
                    .ToList();

                var katalog = await KatalogYukleAsync(ilgiliHastaneler, ct);
                var eskiSinir = DateTime.Now.AddDays(-EskiSiparisGun);

                foreach (var apiOrder in cevap.Orders.OrderBy(o => o.OrderId))
                {
                    var externalRef = apiOrder.OrderId.ToString();

                    if (mevcutRefSet.Contains(externalRef))
                    {
                        sonuc.MevcutAtlanan++;
                        continue;
                    }

                    var musteriKodu = (apiOrder.CustomerCode ?? "").Trim().ToUpperInvariant();
                    if (string.IsNullOrEmpty(musteriKodu))
                    {
                        sonuc.EslesmeyokAtlanan++;
                        continue;
                    }

                    var eslesme = cozucu.Bul(apiOrder);
                    if (eslesme == null)
                    {
                        var anahtar = SiparisMusteriCozucu.Anahtar(apiOrder);
                        if (!eslesmeyenToplam.TryGetValue(anahtar, out var mevcut))
                        {
                            eslesmeyenToplam[anahtar] = new EslesmeyenMusteri
                            {
                                Anahtar = anahtar,
                                MusteriKodu = musteriKodu,
                                OrtakKod = cozucu.OrtakKod(musteriKodu),
                                KurumAdi = apiOrder.HospitalName,
                                MusteriAdi = apiOrder.CustomerName,
                                SiparisSayisi = 1,
                                SonOrderId = apiOrder.OrderId
                            };
                        }
                        else
                        {
                            mevcut.SiparisSayisi++;
                            if (apiOrder.OrderId > mevcut.SonOrderId) mevcut.SonOrderId = apiOrder.OrderId;
                        }
                        sonuc.EslesmeyokAtlanan++;
                        continue;
                    }

                    if (eslesme.Yoksay || !eslesme.InternalHospitalID.HasValue)
                    {
                        sonuc.YoksayAtlanan++;
                        continue;
                    }

                    var internalHospitalId = eslesme.InternalHospitalID.Value;

                    if (apiOrder.Items == null || apiOrder.Items.Count == 0)
                    {
                        sonuc.KalemiOlmayanAtlanan++;
                        continue;
                    }

                    // ── Tarih ──
                    // Okunamazsa "bugün" kabul edilir. Eski kod okunamayan tarihi
                    // 01.01.0001 bırakıp siparişi "çok eski" diye sessizce atlıyordu.
                    if (!TarihOku(apiOrder.OrderDate, out var orderDate))
                    {
                        orderDate = DateTime.Now;
                        sonuc.TarihOkunamayan++;
                        _log.LogWarning("Sipariş tarihi okunamadı, bugün kabul edildi. OrderID={OrderId} Ham='{Ham}'",
                            apiOrder.OrderId, apiOrder.OrderDate);
                    }

                    if (orderDate < eskiSinir)
                    {
                        sonuc.EskiAtlanan++;
                        continue;
                    }

                    var talep = new SiparisTalep
                    {
                        HospitalID = internalHospitalId,
                        TalepTarihi = orderDate,
                        HedefDonem = new DateTime(orderDate.Year, orderDate.Month, 1),
                        ExternalRefNo = externalRef,
                        HastaneNotu = string.IsNullOrWhiteSpace(apiOrder.Note) ? null : Kirp(apiOrder.Note.Trim(), 1000),
                        Durum = SiparisTalepDurumu.Beklemede,
                        OlusturmaTarihi = DateTime.Now
                    };

                    foreach (var apiItem in apiOrder.Items)
                    {
                        var kalem = KalemOlustur(katalog, internalHospitalId, apiItem);
                        talep.Kalemler.Add(kalem);

                        sonuc.YeniKalem++;
                        if (kalem.TestID.HasValue) sonuc.EslesenKalem++;
                        else sonuc.EslesmeyenKalem++;

                        var ambalaj = string.IsNullOrWhiteSpace(apiItem.AmbalajAdi) ? "(boş)" : apiItem.AmbalajAdi.Trim();
                        sonuc.AmbalajDagilimi.TryGetValue(ambalaj, out var adet);
                        sonuc.AmbalajDagilimi[ambalaj] = adet + 1;
                    }

                    _db.SiparisTalepler.Add(talep);
                    await _db.SaveChangesAsync(ct);

                    yeniTalepIdler.Add(talep.ID);
                    sonuc.YeniKaydedilen++;
                    mevcutRefSet.Add(externalRef);
                }

                sonuc.Basarili = true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Sipariş sync HATASI");
                sonuc.HataMesaji = EnIcMesaj(ex);
            }

            // ── Öneri ve hüküm: tüm bekleyenler için tek seferde ──
            if (sonuc.Basarili || yeniTalepIdler.Count > 0)
            {
                try
                {
                    await _dogrulama.YenidenHesaplaAsync(null, ct);

                    if (yeniTalepIdler.Count > 0)
                    {
                        sonuc.OtomatikOnerilenKalem = await _db.SiparisTalepKalemleri.AsNoTracking()
                            .CountAsync(k => yeniTalepIdler.Contains(k.SiparisTalepID)
                                          && k.DogrulamaRengi != DogrulamaRengi.VeriYok, ct);
                    }
                }
                catch (Exception ex)
                {
                    // Siparişler kaydedildi; öneri bir sonraki ekran açılışında hesaplanır
                    _log.LogError(ex, "Sipariş sync: öneri hesabı başarısız");
                }
            }

            sonuc.EslesmeyenMusteriler = eslesmeyenToplam.Values
                .OrderByDescending(x => x.SiparisSayisi)
                .ToList();

            sonuc.BitisZamani = DateTime.Now;
            _log.LogInformation("Sipariş sync bitti. {Ozet}", sonuc.Ozet);
            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════
        //  API'DEN OKUMA (yalnızca GET)
        // ═══════════════════════════════════════════════════════════
        //  Sipariş yazılımı "bekleyen" siparişleri EN YENİDEN ESKİYE doğru,
        //  en fazla 500 tane döndürür. Orada siparişler işlendikten sonra da
        //  "bekliyor" kaldığından bu liste hep doludur; bu tek başına sorun
        //  değildir. Sorun ancak dönen EN ESKİ sipariş hâlâ alım penceresinin
        //  (60 gün) içindeyse vardır: o zaman pencere içinde görülemeyen
        //  siparişler olabilir.
        private async Task<(SiparisApiResponseDto? Cevap, bool SinirdaKaldi)> TumSiparisleriCekAsync(CancellationToken ct)
        {
            var cevap = await _api.GetOrdersAsync(status: 1, sinceId: 0, limit: ApiLimit, ct: ct);
            if (cevap == null || !cevap.Success) return (null, false);

            var sinirda = false;
            if (cevap.Orders.Count >= ApiLimit)
            {
                var eskiSinir = DateTime.Now.AddDays(-EskiSiparisGun);
                var enEski = DateTime.MaxValue;
                foreach (var o in cevap.Orders)
                    if (TarihOku(o.OrderDate, out var t) && t < enEski) enEski = t;

                sinirda = enEski >= eskiSinir;
            }

            return (cevap, sinirda);
        }

        // ═══════════════════════════════════════════════════════════
        //  TEŞHİS — "bu sipariş neden gelmedi?"  (hiçbir şey yazmaz)
        // ═══════════════════════════════════════════════════════════
        public async Task<SiparisTeshisSonuc> TeshisAsync(CancellationToken ct = default)
        {
            var sonuc = new SiparisTeshisSonuc();

            try
            {
                var (cevap, sinirda) = await TumSiparisleriCekAsync(ct);
                if (cevap == null) return sonuc;

                sonuc.ApiyeUlasildi = true;
                sonuc.ApiSinirinaTakildi = sinirda;
                sonuc.ApidekiSiparis = cevap.Orders.Count;

                var kayitli = await _db.SiparisTalepler.AsNoTracking()
                    .Where(t => t.ExternalRefNo != null)
                    .Select(t => t.ExternalRefNo!)
                    .ToListAsync(ct);
                var kayitliSet = new HashSet<string>(kayitli, StringComparer.OrdinalIgnoreCase);

                var eslesmeler = await _db.HospitalEslesmeleri.AsNoTracking().ToListAsync(ct);
                var cozucu = new SiparisMusteriCozucu(eslesmeler, cevap.Orders);
                sonuc.OrtakKodlar = cozucu.OrtakKodlar.OrderBy(x => x).ToList();
                sonuc.GecersizEskiEslesmeler = eslesmeler
                    .Where(cozucu.GecersizEskiEslesme)
                    .Select(e => e.ExternalMusteriKodu)
                    .ToList();

                var eskiSinir = DateTime.Now.AddDays(-EskiSiparisGun);
                var musteriler = new Dictionary<string, EslesmeyenMusteri>(StringComparer.OrdinalIgnoreCase);

                foreach (var o in cevap.Orders.OrderByDescending(x => x.OrderId))
                {
                    if (kayitliSet.Contains(o.OrderId.ToString()))
                    {
                        sonuc.Alinmis++;
                        continue;
                    }

                    var kod = (o.CustomerCode ?? "").Trim().ToUpperInvariant();
                    var tarihOkundu = TarihOku(o.OrderDate, out var tarih);

                    string neden, cozum;
                    if (string.IsNullOrEmpty(kod))
                    {
                        neden = "Müşteri kodu boş";
                        cozum = "Sipariş yazılımında müşteriye kod tanımlanmalı.";
                    }
                    else if (cozucu.Bul(o) is not { } e)
                    {
                        neden = cozucu.OrtakKod(kod)
                            ? "Kurum hiçbir hastaneye bağlanmamış (müşteri kodu başka kurumlarla ortak)"
                            : "Müşteri hiçbir hastaneye bağlanmamış";
                        cozum = "Hastane Eşleştirme ekranında bu kurumu bir hastaneye bağlayın.";

                        var anahtar = SiparisMusteriCozucu.Anahtar(o);
                        if (!musteriler.TryGetValue(anahtar, out var m))
                            musteriler[anahtar] = m = new EslesmeyenMusteri
                            {
                                Anahtar = anahtar,
                                MusteriKodu = kod,
                                OrtakKod = cozucu.OrtakKod(kod),
                                KurumAdi = o.HospitalName,
                                MusteriAdi = o.CustomerName
                            };
                        m.SiparisSayisi++;
                        if (o.OrderId > m.SonOrderId) m.SonOrderId = o.OrderId;
                    }
                    else if (e.Yoksay || !e.InternalHospitalID.HasValue)
                    {
                        neden = "Müşteri \"yoksay\" olarak işaretli";
                        cozum = "Takip edilecekse Hastane Eşleşmesi ekranında yoksay'ı kaldırıp hastane seçin.";
                    }
                    else if (o.Items == null || o.Items.Count == 0)
                    {
                        neden = "Siparişte ürün kalemi yok";
                        cozum = "Sipariş yazılımında siparişin kalemleri kontrol edilmeli.";
                    }
                    else if (tarihOkundu && tarih < eskiSinir)
                    {
                        // Bilerek alınmıyor — tek tek listelemek ekranı boğar
                        sonuc.EskiOlduguIcinAlinmayan++;
                        continue;
                    }
                    else
                    {
                        neden = "Henüz çekilmedi";
                        cozum = "\"Siparişleri Çek\" düğmesine basınca alınır.";
                        sonuc.CekilmeyiBekleyen++;
                    }

                    sonuc.Gelmeyenler.Add(new GelmeyenSiparis
                    {
                        OrderId = o.OrderId,
                        MusteriKodu = kod,
                        KurumAdi = o.HospitalName ?? o.CustomerName,
                        Tarih = tarihOkundu ? tarih : null,
                        KalemSayisi = o.Items?.Count ?? 0,
                        Neden = neden,
                        Cozum = cozum
                    });
                }

                sonuc.EslesmeyenMusteriler = musteriler.Values
                    .OrderByDescending(x => x.SiparisSayisi).ToList();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Sipariş teşhisi başarısız");
                sonuc.HataMesaji = EnIcMesaj(ex);
            }

            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════
        //  YENİDEN EŞLEŞTİR — bekleyen talepler
        // ═══════════════════════════════════════════════════════════
        public async Task<YenidenEslestirSonuc> YenidenEslestirAsync(CancellationToken ct = default)
        {
            var sonuc = new YenidenEslestirSonuc();

            try
            {
                var bekleyenler = await _db.SiparisTalepler
                    .Include(t => t.Kalemler)
                    .Where(t => t.Durum == SiparisTalepDurumu.Beklemede)
                    .ToListAsync(ct);

                if (bekleyenler.Count == 0)
                {
                    sonuc.Basarili = true;
                    return sonuc;
                }

                var katalog = await KatalogYukleAsync(
                    bekleyenler.Select(t => t.HospitalID).Distinct().ToList(), ct);

                // Sipariş yazılımına ulaşılabiliyorsa miktar + ambalajı oradan tazele
                var apiSiparisler = new Dictionary<string, SiparisApiOrderDto>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var (cevap, _) = await TumSiparisleriCekAsync(ct);
                    if (cevap != null)
                    {
                        sonuc.ApiyeUlasildi = true;
                        foreach (var o in cevap.Orders)
                            apiSiparisler[o.OrderId.ToString()] = o;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Yeniden eşleştirme: sipariş yazılımına ulaşılamadı, saklanan bilgiyle devam.");
                }

                foreach (var talep in bekleyenler)
                {
                    SiparisApiOrderDto? apiOrder = null;
                    if (!string.IsNullOrEmpty(talep.ExternalRefNo))
                        apiSiparisler.TryGetValue(talep.ExternalRefNo, out apiOrder);

                    // Aynı ürün siparişte iki kez geçebilir — her API kalemi bir kez kullanılır
                    var kullanilan = new HashSet<int>();

                    foreach (var kalem in talep.Kalemler.OrderBy(k => k.ID))
                    {
                        sonuc.IncelenenKalem++;

                        var apiItem = apiOrder?.Items?
                            .Where(i => i.ProductId == kalem.ExternalProductId && !kullanilan.Contains(i.ItemId))
                            .OrderByDescending(i => kalem.HastaneTalepKutu.HasValue && i.Miktar == kalem.HastaneTalepKutu.Value)
                            .FirstOrDefault();

                        if (apiItem != null) kullanilan.Add(apiItem.ItemId);

                        var refNo = apiItem?.ReferenceNumber ?? kalem.ReferenceNumber;
                        var ad = apiItem?.ProductName ?? kalem.ProductName;

                        var (test, snap, _) = Eslestir(katalog, talep.HospitalID, refNo, ad);

                        var eskiTestId = kalem.TestID;
                        // Daha önce eşleşmiş kalemin eşleşmesi geri alınmaz
                        var yeniTestId = test?.TestID ?? snap?.MappedTestID ?? eskiTestId;

                        if (test == null && yeniTestId.HasValue)
                            katalog.ById.TryGetValue(yeniTestId.Value, out test);

                        var yeniTalepTest = kalem.HastaneTalepTest;
                        var yeniTalepKutu = kalem.HastaneTalepKutu;

                        if (apiItem != null)
                        {
                            (yeniTalepTest, yeniTalepKutu) = HesaplaTalep(apiItem.Miktar, apiItem.AmbalajAdi, test, snap);
                            sonuc.ApidenTazelenen++;
                        }
                        else if (yeniTestId != eskiTestId && kalem.HastaneTalepKutu.HasValue)
                        {
                            // Ürün yeni eşleşti: saklanan "kutu" hastanenin girdiği ham miktardır,
                            // yeni ürünün kutu içi adediyle teste çevir.
                            (yeniTalepTest, yeniTalepKutu) = HesaplaTalep(kalem.HastaneTalepKutu.Value, null, test, snap);
                        }

                        var yeniTip = UrunTipiBelirle(katalog, test, snap, ad);

                        var degisti = yeniTestId != eskiTestId
                                   || yeniTalepTest != kalem.HastaneTalepTest
                                   || yeniTalepKutu != kalem.HastaneTalepKutu
                                   || yeniTip != kalem.UrunTipi;

                        if (degisti)
                        {
                            kalem.TestID = yeniTestId;
                            kalem.HastaneTalepTest = yeniTalepTest;
                            kalem.HastaneTalepKutu = yeniTalepKutu;
                            kalem.UrunTipi = yeniTip;

                            if (!eskiTestId.HasValue && yeniTestId.HasValue) sonuc.YeniEslesen++;
                            else sonuc.Degisen++;
                        }

                        if (!yeniTestId.HasValue) sonuc.HalaEslesmeyen++;
                    }
                }

                await _db.SaveChangesAsync(ct);
                await _dogrulama.YenidenHesaplaAsync(null, ct);

                sonuc.Basarili = true;
                _log.LogInformation("Yeniden eşleştirme bitti. {Ozet}", sonuc.Ozet);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Yeniden eşleştirme HATASI");
                sonuc.HataMesaji = EnIcMesaj(ex);
            }

            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════
        //  KATALOG + EŞLEŞTİRME
        // ═══════════════════════════════════════════════════════════

        private class Katalog
        {
            public Dictionary<int, TestBirimInfo> ById { get; set; } = new();
            public Dictionary<string, TestBirimInfo> ByRef { get; set; } = new();

            /// <summary>Normalize ad → test. Aynı ad birden çok testte varsa değer null (belirsiz).</summary>
            public Dictionary<string, TestBirimInfo?> ByAd { get; set; } = new();

            public HashSet<int> Takipli { get; set; } = new();

            /// <summary>Hastane → (normalize referans → sayımdaki ürün).</summary>
            public Dictionary<int, Dictionary<string, SnapKisa>> SnapByRef { get; set; } = new();

            /// <summary>Hastane → (normalize ad → sayımdaki ürün).</summary>
            public Dictionary<int, Dictionary<string, SnapKisa>> SnapByAd { get; set; } = new();
        }

        private async Task<Katalog> KatalogYukleAsync(List<int> hospitalIds, CancellationToken ct)
        {
            var k = new Katalog();

            var testler = await _db.Tests.AsNoTracking()
                .Select(t => new TestBirimInfo
                {
                    TestID = t.TestID,
                    TestName = t.TestName,
                    SutCode = t.SutCode,
                    Kategori = t.Kategori,
                    GirisTipi = t.GirisTipi,
                    KutuBasinaAdet = t.KutuBasinaAdet
                })
                .ToListAsync(ct);

            k.ById = testler.ToDictionary(t => t.TestID);

            foreach (var t in testler)
            {
                // SutCode birden çok referans taşıyabilir: "8P4530, 8P4532"
                foreach (var refKey in ReferansKodu.Kodlar(t.SutCode))
                    k.ByRef.TryAdd(refKey, t);

                var adKey = ReferansKodu.AdNormalize(t.TestName);
                if (adKey.Length >= 4)
                {
                    // Aynı adla iki test varsa ad eşleşmesi güvenilmez — null ile işaretle
                    k.ByAd[adKey] = k.ByAd.ContainsKey(adKey) ? null : t;
                }
            }

            var takipli = await _db.MonthlyStatistics.AsNoTracking()
                .Select(s => s.TestID)
                .Distinct()
                .ToListAsync(ct);
            k.Takipli = takipli.ToHashSet();

            if (hospitalIds.Count > 0)
            {
                var snaplar = await _db.FizikiSayimSnapshotlari.AsNoTracking()
                    .Where(s => hospitalIds.Contains(s.HospitalID) && s.SayimAktifMi)
                    .Select(s => new SnapKisa
                    {
                        HospitalID = s.HospitalID,
                        ReferenceNumber = s.ReferenceNumber,
                        ProductName = s.ProductName,
                        MappedTestID = s.MappedTestID,
                        UrunTipi = s.UrunTipi,
                        BoxTextCount = s.BoxTextCount,
                        FlaconCountPerBox = s.FlaconCountPerBox
                    })
                    .ToListAsync(ct);

                foreach (var grup in snaplar.GroupBy(s => s.HospitalID))
                {
                    var byRef = new Dictionary<string, SnapKisa>();
                    var byAd = new Dictionary<string, SnapKisa>();

                    // Eşleşmiş olanlar önce — aynı referansın iki lot'undan eşleşeni seç
                    foreach (var s in grup.OrderByDescending(x => x.MappedTestID.HasValue))
                    {
                        var refKey = ReferansKodu.Normalize(s.ReferenceNumber);
                        if (refKey.Length > 0 && !byRef.ContainsKey(refKey)) byRef[refKey] = s;

                        var adKey = ReferansKodu.AdNormalize(s.ProductName);
                        if (adKey.Length >= 4 && !byAd.ContainsKey(adKey)) byAd[adKey] = s;
                    }

                    k.SnapByRef[grup.Key] = byRef;
                    k.SnapByAd[grup.Key] = byAd;
                }
            }

            return k;
        }

        /// <summary>
        /// Sipariş kalemini katalogdaki ürünle eşleştirir.
        /// Sıra: hastanenin sayımındaki ürün → katalog referansı → birebir ad.
        /// </summary>
        private static (TestBirimInfo? Test, SnapKisa? Snap, UrunEslesme Nasil) Eslestir(
            Katalog k, int hospitalId, string? referans, string? urunAdi)
        {
            var refKey = ReferansKodu.Normalize(referans);
            var adKey = ReferansKodu.AdNormalize(urunAdi);

            SnapKisa? snap = null;
            if (refKey.Length > 0 && k.SnapByRef.TryGetValue(hospitalId, out var byRef))
                byRef.TryGetValue(refKey, out snap);

            // 1) Hastanenin sayımında aynı referansla duran ürün
            if (snap?.MappedTestID != null && k.ById.TryGetValue(snap.MappedTestID.Value, out var t1))
                return (t1, snap, UrunEslesme.Sayim);

            // 2) Katalog referansı
            if (refKey.Length > 0 && k.ByRef.TryGetValue(refKey, out var t2))
                return (t2, snap, UrunEslesme.Referans);

            // 3) Birebir ad — önce sayımdaki ürün adı, sonra katalog adı
            if (adKey.Length >= 4)
            {
                if (k.SnapByAd.TryGetValue(hospitalId, out var byAd)
                    && byAd.TryGetValue(adKey, out var adSnap)
                    && adSnap.MappedTestID != null
                    && k.ById.TryGetValue(adSnap.MappedTestID.Value, out var t3))
                {
                    return (t3, snap ?? adSnap, UrunEslesme.Ad);
                }

                if (k.ByAd.TryGetValue(adKey, out var t4) && t4 != null)
                    return (t4, snap, UrunEslesme.Ad);
            }

            return (null, snap, UrunEslesme.Yok);
        }

        private static UrunTipi UrunTipiBelirle(Katalog k, TestBirimInfo? test, SnapKisa? snap, string? urunAdi)
        {
            if (snap != null) return snap.UrunTipi;

            if (test != null)
            {
                if (k.Takipli.Contains(test.TestID)) return UrunTipi.Reaktif;
                return UrunTipiTespitHelper.Tespit(test.TestName, test.Kategori);
            }

            return UrunTipiTespitHelper.Tespit(urunAdi, null);
        }

        private static SiparisTalepKalem KalemOlustur(Katalog katalog, int hospitalId, SiparisApiItemDto apiItem)
        {
            var (test, snap, _) = Eslestir(katalog, hospitalId, apiItem.ReferenceNumber, apiItem.ProductName);
            var (talepTest, talepKutu) = HesaplaTalep(apiItem.Miktar, apiItem.AmbalajAdi, test, snap);

            return new SiparisTalepKalem
            {
                ExternalProductId = apiItem.ProductId,
                ProductName = Kirp(string.IsNullOrWhiteSpace(apiItem.ProductName) ? "(ad yok)" : apiItem.ProductName.Trim(), 200),
                ReferenceNumber = Kirp((apiItem.ReferenceNumber ?? "").Trim(), 50),
                TestID = test?.TestID ?? snap?.MappedTestID,
                UrunTipi = UrunTipiBelirle(katalog, test, snap, apiItem.ProductName),
                HastaneTalepKutu = talepKutu,
                HastaneTalepTest = talepTest,
                DogrulamaRengi = DogrulamaRengi.VeriYok,
                OlusturmaTarihi = DateTime.Now
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  MİKTAR — hastanenin girdiği sayıyı teste çevir
        // ═══════════════════════════════════════════════════════════
        //
        //  Sipariş yazılımındaki ambalaj adları: Kutu, Adet, Test ve boş.
        //  Dördü de "kaç paket" demektir — miktar × kutu içi test.
        //
        //  DİKKAT: Ambalaj adı "Test" olan kalemler test ADEDİ değildir.
        //  Örn. "Anti-HCV Next (2x500T)" ambalaj=Test, miktar=3 → 3 kit.
        //  Bir önceki sürüm bunu "3 test" sayıyor, bu kalemleri hep
        //  "çok az istenmiş" gösteriyordu.
        //
        //  Ambalaj "flakon"    → kutu içi test ÷ kutu içi flakon × miktar
        //                        (flakon bilgisi yoksa kutu gibi sayılır)
        //
        //  Dönen "kutu" değeri yalnızca hastane KUTU cinsinden girdiyse doludur.
        // ═══════════════════════════════════════════════════════════
        private static (int TalepTest, int? TalepKutu) HesaplaTalep(
            int miktar, string? ambalajAdi, TestBirimInfo? test, SnapKisa? snap)
        {
            if (miktar < 0) miktar = 0;

            // 1 kutu kaç test
            int kutuIci;
            if (test != null && test.GirisTipi == TestGirisTipi.Kutu && test.KutuBasinaAdet.GetValueOrDefault() > 0)
                kutuIci = test.KutuBasinaAdet!.Value;
            else if (test != null && test.GirisTipi == TestGirisTipi.Adet)
                kutuIci = 1;
            else if (snap != null && snap.BoxTextCount > 0)
                kutuIci = snap.BoxTextCount;
            else
                kutuIci = 1;

            var ambalaj = (ambalajAdi ?? "").Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));

            if (ambalaj.Contains("flakon") || ambalaj.Contains("flacon") || ambalaj.Contains("vial") || ambalaj.Contains("şişe"))
            {
                var flakonPerKutu = snap?.FlaconCountPerBox ?? 0;
                if (kutuIci > 1 && flakonPerKutu > 1)
                {
                    var test1Flakon = (decimal)kutuIci / flakonPerKutu;
                    return ((int)Math.Round(miktar * test1Flakon, MidpointRounding.AwayFromZero), null);
                }
                // Kutu içi flakon sayısı bilinmiyor — kutu gibi değerlendir
            }

            return (miktar * kutuIci, miktar);
        }

        // ═══════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════

        private static readonly string[] TarihBicimleri =
        {
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
            "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
            "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"
        };

        private static bool TarihOku(string? ham, out DateTime tarih)
        {
            tarih = default;
            if (string.IsNullOrWhiteSpace(ham)) return false;

            var s = ham.Trim();

            if (DateTime.TryParseExact(s, TarihBicimleri, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out tarih) && tarih.Year > 2000)
                return true;

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out tarih)
                && tarih.Year > 2000)
                return true;

            tarih = default;
            return false;
        }

        private static string Kirp(string s, int enFazla) =>
            s.Length <= enFazla ? s : s.Substring(0, enFazla);

        private static string EnIcMesaj(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }

        private class TestBirimInfo
        {
            public int TestID { get; set; }
            public string TestName { get; set; } = "";
            public string? SutCode { get; set; }
            public string? Kategori { get; set; }
            public TestGirisTipi GirisTipi { get; set; }
            public int? KutuBasinaAdet { get; set; }
        }

        private class SnapKisa
        {
            public int HospitalID { get; set; }
            public string ReferenceNumber { get; set; } = "";
            public string ProductName { get; set; } = "";
            public int? MappedTestID { get; set; }
            public UrunTipi UrunTipi { get; set; }
            public int BoxTextCount { get; set; }
            public int FlaconCountPerBox { get; set; }
        }
    }
}
