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
    public class SiparisSyncService : ISiparisSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly ISiparisApiClient _api;
        private readonly IStokHesaplamaService _stokHesap;
        private readonly ILogger<SiparisSyncService> _log;

        public SiparisSyncService(
            ApplicationDbContext db,
            ISiparisApiClient api,
            IStokHesaplamaService stokHesap,
            ILogger<SiparisSyncService> log)
        {
            _db = db;
            _api = api;
            _stokHesap = stokHesap;
            _log = log;
        }

        public async Task<SiparisSyncSonuc> SyncAsync(CancellationToken ct = default)
        {
            var sonuc = new SiparisSyncSonuc();

       
            var eslesmeyenToplam = new Dictionary<string, EslesmeyenMusteri>(StringComparer.OrdinalIgnoreCase);

            try
            {

                var mevcutExternalRefler = await _db.SiparisTalepler
                    .AsNoTracking()
                    .Where(s => s.ExternalRefNo != null)
                    .Select(s => s.ExternalRefNo!)
                    .ToListAsync(ct);

                _log.LogInformation("Sipariş sync başlıyor. KayıtlıRefSayısı={N}", mevcutExternalRefler.Count);

                // 2) API'den TÜM bekleyen siparişleri çek 
                var cevap = await _api.GetOrdersAsync(status: 1, sinceId: 0, limit: 500, ct: ct);
                if (cevap == null || !cevap.Success)
                {
                    sonuc.HataMesaji = "Sipariş API cevap vermedi.";
                    sonuc.BitisZamani = DateTime.Now;
                    return sonuc;
                }

                sonuc.ApidanGelenToplam = cevap.Orders.Count;
                if (cevap.Orders.Count == 0)
                {
                    sonuc.Basarili = true;
                    sonuc.BitisZamani = DateTime.Now;
                    return sonuc;
                }

                // 3) Eşleştirme tablosu
                var tumEslesmeler = await _db.HospitalEslesmeleri
                    .AsNoTracking()
                    .ToListAsync(ct);
                var eslesmeMap = tumEslesmeler
                    .GroupBy(e => (e.ExternalMusteriKodu ?? "").Trim().ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.First());

                var mevcutRefSet = new HashSet<string>(
                    mevcutExternalRefler.Where(x => !string.IsNullOrEmpty(x)),
                    StringComparer.OrdinalIgnoreCase);
                

            
                var tumTestler = await _db.Tests
                    .AsNoTracking()
                    .Select(t => new TestBirimInfo
                    {
                        TestID = t.TestID,
                        TestName = t.TestName,
                        SutCode = t.SutCode,
                        GirisTipi = t.GirisTipi,
                        KutuBasinaAdet = t.KutuBasinaAdet
                    })
                    .ToListAsync(ct);

                var testByRef = new Dictionary<string, TestBirimInfo>();
                foreach (var t in tumTestler)
                {
                    var key = NormalizeRef(t.SutCode);
                    if (!string.IsNullOrEmpty(key) && !testByRef.ContainsKey(key))
                        testByRef[key] = t;
                }
                var testById = tumTestler.ToDictionary(t => t.TestID);

              
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

                    if (!eslesmeMap.TryGetValue(musteriKodu, out var eslesme))
                    {
                        if (!eslesmeyenToplam.TryGetValue(musteriKodu, out var mevcut))
                        {
                            eslesmeyenToplam[musteriKodu] = new EslesmeyenMusteri
                            {
                                MusteriKodu = musteriKodu,
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

                   
                    DateTime orderDate = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(apiOrder.OrderDate))
                        DateTime.TryParseExact(apiOrder.OrderDate, "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out orderDate);

                   
                    var eskiSinir = DateTime.Now.AddDays(-60);
                    if (orderDate < eskiSinir)
                    {
                        _log.LogInformation("Eski sipariş atlandı. OrderID={OrderId} Tarih={Tarih:dd.MM.yyyy}",
                            apiOrder.OrderId, orderDate);
                        continue;
                    }

                    var talep = new SiparisTalep
                    {
                        HospitalID = internalHospitalId,
                        TalepTarihi = orderDate,
                        HedefDonem = new DateTime(orderDate.Year, orderDate.Month, 1),
                        ExternalRefNo = externalRef,
                        HastaneNotu = string.IsNullOrWhiteSpace(apiOrder.Note) ? null : apiOrder.Note,
                        Durum = SiparisTalepDurumu.Beklemede,
                        OlusturmaTarihi = DateTime.Now
                    };
                    _db.SiparisTalepler.Add(talep);
                    await _db.SaveChangesAsync(ct);

              
                    foreach (var apiItem in apiOrder.Items)
                    {
                        var kalemi = await KalemiOlustur(
                            internalHospitalId, apiItem, testByRef, testById, ct);
                        kalemi.SiparisTalepID = talep.ID;
                        _db.SiparisTalepKalemleri.Add(kalemi);

                        if (kalemi.OnerilenTest > 0 || kalemi.DogrulamaRengi != DogrulamaRengi.VeriYok)
                            sonuc.OtomatikOnerilenKalem++;
                    }

                    await _db.SaveChangesAsync(ct);
                    sonuc.YeniKaydedilen++;
                    mevcutRefSet.Add(externalRef);
                }

                sonuc.Basarili = true;
                sonuc.BitisZamani = DateTime.Now;
                _log.LogInformation("Sipariş sync bitti. {Ozet}", sonuc.Ozet);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Sipariş sync HATASI");
                sonuc.HataMesaji = ex.Message;
                sonuc.BitisZamani = DateTime.Now;
            }

            
            sonuc.EslesmeyenMusteriler = eslesmeyenToplam.Values
                .OrderByDescending(x => x.SiparisSayisi)
                .ToList();

            return sonuc;
        }


        private async Task<SiparisTalepKalem> KalemiOlustur(
            int hospitalId,
            SiparisApiItemDto apiItem,
            Dictionary<string, TestBirimInfo> testByRef,
            Dictionary<int, TestBirimInfo> testById,
            CancellationToken ct)
        {
            var refNorm = NormalizeRef(apiItem.ReferenceNumber);

        
            var snapshots = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => s.HospitalID == hospitalId && s.SayimAktifMi)
                .ToListAsync(ct);
            var snap = snapshots.FirstOrDefault(s => NormalizeRef(s.ReferenceNumber) == refNorm);

         
            TestBirimInfo? test = null;
            if (snap?.MappedTestID.HasValue == true
                && testById.TryGetValue(snap.MappedTestID.Value, out var mapped))
            {
                test = mapped;
            }
            else if (!string.IsNullOrEmpty(refNorm)
                     && testByRef.TryGetValue(refNorm, out var found))
            {
                test = found;
            }

            var (talepTest, talepKutu) = HesaplaTalep(apiItem.Miktar, test, snap);

            var kalem = new SiparisTalepKalem
            {
                ExternalProductId = apiItem.ProductId,
                ProductName = apiItem.ProductName ?? "(ad yok)",
                ReferenceNumber = apiItem.ReferenceNumber ?? "",
                TestID = test?.TestID ?? snap?.MappedTestID,
                UrunTipi = snap?.UrunTipi ?? UrunTipi.Diger,
                HastaneTalepKutu = talepKutu,
                HastaneTalepTest = talepTest,
                DogrulamaRengi = DogrulamaRengi.VeriYok,
                OlusturmaTarihi = DateTime.Now
            };

            var effectiveTestId = test?.TestID ?? snap?.MappedTestID;
            if (snap != null && snap.UrunTipi == UrunTipi.Reaktif && effectiveTestId.HasValue)
            {
                try
                {
                    var hesap = await _stokHesap.HesaplaUrunAsync(hospitalId, effectiveTestId.Value, 3, ct);
                    if (hesap != null)
                    {
                        kalem.HesapGuncelStok = hesap.HesapGuncelStok;
                        kalem.OrtalamaAylikKullanim = hesap.OrtalamaAylikKullanim;
                        kalem.OnerilenTest = hesap.OnerilenTest;
                        kalem.OnerilenKutu = hesap.OnerilenKutu;
                        kalem.HesaplamaAciklamasi = hesap.HesaplamaOzeti;

                        if (hesap.OnerilenTest > 0)
                        {
                            var sapma = (decimal)(kalem.HastaneTalepTest - hesap.OnerilenTest)
                                        / hesap.OnerilenTest * 100m;
                            kalem.SapmaYuzde = Math.Round(sapma, 2);
                            kalem.DogrulamaRengi = SapmaRengi(sapma);
                        }
                        else if (kalem.HastaneTalepTest > 0)
                        {
                     
                            kalem.SapmaYuzde = 999;
                            kalem.DogrulamaRengi = DogrulamaRengi.Kirmizi;
                        }
                        else
                        {
                            kalem.DogrulamaRengi = DogrulamaRengi.Yesil;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "StokHesaplama hatası. TestID={TestID}", effectiveTestId);
                }
            }

            return kalem;
        }

        private static (int talepTest, int? talepKutu) HesaplaTalep(
            int phpMiktar, TestBirimInfo? test, FizikiSayimSnapshot? snap)
        {
      
            if (test != null
                && test.GirisTipi == TestGirisTipi.Kutu
                && test.KutuBasinaAdet.HasValue
                && test.KutuBasinaAdet.Value > 0)
            {
                return (phpMiktar * test.KutuBasinaAdet.Value, phpMiktar);
            }

 
            if (test != null && test.GirisTipi == TestGirisTipi.Adet)
            {
                return (phpMiktar, phpMiktar);
            }


            if (snap != null && snap.BoxTextCount > 0)
            {
                return (phpMiktar * snap.BoxTextCount, phpMiktar);
            }

            return (phpMiktar, phpMiktar);
        }

        private static DogrulamaRengi SapmaRengi(decimal sapmaYuzde)
        {
            var abs = Math.Abs(sapmaYuzde);
            if (abs <= 10) return DogrulamaRengi.Yesil;
            if (abs <= 30) return DogrulamaRengi.Sari;
            return DogrulamaRengi.Kirmizi;
        }

        private static string NormalizeRef(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var cleaned = s.Trim().ToUpperInvariant().TrimStart('0');
            return cleaned.Length == 0 ? "0" : cleaned;
        }


        private class TestBirimInfo
        {
            public int TestID { get; set; }
            public string TestName { get; set; } = "";
            public string? SutCode { get; set; }
            public TestGirisTipi GirisTipi { get; set; }
            public int? KutuBasinaAdet { get; set; }
        }
    }
}