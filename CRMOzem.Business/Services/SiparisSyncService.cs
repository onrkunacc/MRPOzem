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
using System.Text;
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

            try
            {
                // 1) Mevcut en yüksek ExternalRefNo bul → sadece yenileri çek
                var lastSyncedOrderIdStr = await _db.SiparisTalepler
                    .AsNoTracking()
                    .Where(s => s.ExternalRefNo != null)
                    .Select(s => s.ExternalRefNo)
                    .ToListAsync(ct);

                int sinceId = 0;
                foreach (var s in lastSyncedOrderIdStr)
                {
                    if (int.TryParse(s, out var id) && id > sinceId) sinceId = id;
                }

                _log.LogInformation("Sipariş sync başlıyor. sinceId={SinceId}", sinceId);

                // 2) API'den çek
                var cevap = await _api.GetOrdersAsync(status: 1, sinceId: sinceId, limit: 500, ct: ct);
                if (cevap == null || !cevap.Success)
                {
                    sonuc.HataMesaji = "Sipariş API cevap vermedi veya başarısız döndü.";
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

                // 3) Lookup — hastane adı → internal id
                var crmHospitals = await _db.Hospitals
                    .AsNoTracking()
                    .Select(h => new { h.HospitalID, h.HospitalName })
                    .ToListAsync(ct);

                var hospitalByName = crmHospitals
                    .GroupBy(h => NormalizeName(h.HospitalName), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().HospitalID, StringComparer.OrdinalIgnoreCase);

                // Mevcut externalRefNo'ları önceden al — idempotency
                var mevcutRefler = new HashSet<string>(
                    lastSyncedOrderIdStr.Where(x => !string.IsNullOrEmpty(x))!,
                    StringComparer.OrdinalIgnoreCase);

                // 4) Her sipariş için işle
                foreach (var apiOrder in cevap.Orders.OrderBy(o => o.OrderId))
                {
                    var externalRef = apiOrder.OrderId.ToString();

                    // Idempotency
                    if (mevcutRefler.Contains(externalRef))
                    {
                        sonuc.MevcutAtlanan++;
                        continue;
                    }

                    // Hastane eşleşmesi
                    if (string.IsNullOrWhiteSpace(apiOrder.HospitalName))
                    {
                        sonuc.HastaneSizAtlanan++;
                        continue;
                    }

                    if (!hospitalByName.TryGetValue(NormalizeName(apiOrder.HospitalName), out var internalHospitalId))
                    {
                        sonuc.EslesemeyenHastaneler.Add($"{apiOrder.HospitalName} (orderId={apiOrder.OrderId})");
                        sonuc.HastaneSizAtlanan++;
                        continue;
                    }

                    // Kalem yoksa atla
                    if (apiOrder.Items == null || apiOrder.Items.Count == 0)
                    {
                        sonuc.KalemiOlmayanAtlanan++;
                        continue;
                    }

                    // SiparisTalep oluştur
                    DateTime orderDate = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(apiOrder.OrderDate))
                        DateTime.TryParseExact(apiOrder.OrderDate, "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out orderDate);

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
                    await _db.SaveChangesAsync(ct); // kalemler için ID lazım

                    // Her kalem için hesapla
                    foreach (var apiItem in apiOrder.Items)
                    {
                        var kalemi = await KalemiOlustur(internalHospitalId, apiItem, ct);
                        kalemi.SiparisTalepID = talep.ID;
                        _db.SiparisTalepKalemleri.Add(kalemi);

                        if (kalemi.OnerilenTest > 0 || kalemi.DogrulamaRengi != DogrulamaRengi.VeriYok)
                            sonuc.OtomatikOnerilenKalem++;
                    }

                    await _db.SaveChangesAsync(ct);
                    sonuc.YeniKaydedilen++;
                    mevcutRefler.Add(externalRef);
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

            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════════
        //  TEK KALEM — snapshot eşleştir + öneri hesapla
        // ═══════════════════════════════════════════════════════════════
        private async Task<SiparisTalepKalem> KalemiOlustur(
            int hospitalId,
            SiparisApiItemDto apiItem,
            CancellationToken ct)
        {
            var refNorm = NormalizeRef(apiItem.ReferenceNumber);

            // Snapshot bul — bu üründen bu hastanede aktif sayımda var mı?
            var snapshot = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => s.HospitalID == hospitalId && s.SayimAktifMi)
                .ToListAsync(ct);
            var snap = snapshot.FirstOrDefault(s => NormalizeRef(s.ReferenceNumber) == refNorm);

            var kalem = new SiparisTalepKalem
            {
                ExternalProductId = apiItem.ProductId,
                ProductName = apiItem.ProductName ?? "(ad yok)",
                ReferenceNumber = apiItem.ReferenceNumber ?? "",
                TestID = snap?.MappedTestID,
                UrunTipi = snap?.UrunTipi ?? UrunTipi.Diger,
                HastaneTalepKutu = apiItem.Miktar,
                HastaneTalepTest = apiItem.Miktar * (snap?.BoxTextCount > 0 ? snap.BoxTextCount : 1),
                DogrulamaRengi = DogrulamaRengi.VeriYok,
                OlusturmaTarihi = DateTime.Now
            };

            // Reaktif + snapshot var + Test eşleşmiş → öneri hesapla
            if (snap != null
                && snap.UrunTipi == UrunTipi.Reaktif
                && snap.MappedTestID.HasValue)
            {
                try
                {
                    var hesap = await _stokHesap.HesaplaUrunAsync(hospitalId, snap.MappedTestID.Value, 3, ct);
                    if (hesap != null)
                    {
                        kalem.HesapGuncelStok = hesap.HesapGuncelStok;
                        kalem.OrtalamaAylikKullanim = hesap.OrtalamaAylikKullanim;
                        kalem.OnerilenTest = hesap.OnerilenTest;
                        kalem.OnerilenKutu = hesap.OnerilenKutu;
                        kalem.HesaplamaAciklamasi = hesap.HesaplamaOzeti;

                        // Sapma yüzdesi
                        if (hesap.OnerilenTest > 0)
                        {
                            var sapma = (decimal)(kalem.HastaneTalepTest - hesap.OnerilenTest) / hesap.OnerilenTest * 100m;
                            kalem.SapmaYuzde = Math.Round(sapma, 2);
                            kalem.DogrulamaRengi = SapmaRengi(sapma);
                        }
                        else
                        {
                            // Öneri 0 ama hastane istiyor → fazla sipariş sayılır
                            if (kalem.HastaneTalepTest > 0)
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
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "StokHesaplama hatası — TestID={TestId}, HospitalID={HospitalId}", snap.MappedTestID, hospitalId);
                }
            }

            return kalem;
        }

        // ─────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────
        private static DogrulamaRengi SapmaRengi(decimal sapmaYuzde)
        {
            var abs = Math.Abs(sapmaYuzde);
            if (abs <= 10) return DogrulamaRengi.Yesil;
            if (abs <= 30) return DogrulamaRengi.Sari;
            return DogrulamaRengi.Kirmizi;
        }

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            return name
                .Trim()
                .ToLowerInvariant()
                .Replace("ı", "i").Replace("i̇", "i")
                .Replace("ğ", "g").Replace("ü", "u")
                .Replace("ş", "s").Replace("ö", "o")
                .Replace("ç", "c").Replace("  ", " ");
        }

        private static string NormalizeRef(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var cleaned = s.Trim().ToUpperInvariant().TrimStart('0');
            return cleaned.Length == 0 ? "0" : cleaned;
        }
    }
}
