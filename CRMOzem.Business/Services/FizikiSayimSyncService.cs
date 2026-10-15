using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class FizikiSayimSyncService : IFizikiSayimSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly IFizikiSayimApiClient _api;
        private readonly ILogger<FizikiSayimSyncService> _log;

        public FizikiSayimSyncService(
            ApplicationDbContext db,
            IFizikiSayimApiClient api,
            ILogger<FizikiSayimSyncService> log)
        {
            _db = db;
            _api = api;
            _log = log;
        }

        public async Task<FizikiSayimSyncSonuc> SyncAsync(int? hospitalId = null, CancellationToken ct = default)
        {
            var sonuc = new FizikiSayimSyncSonuc();

            try
            {
                _log.LogInformation("Fiziki sayım sync başlıyor. HospitalId={HospitalId}", hospitalId);

                // 1) API'den çek
                var cevap = await _api.GetStocksAsync(hospitalId, null, ct);
                if (cevap == null || !cevap.Success)
                {
                    sonuc.HataMesaji = "API cevap vermedi veya başarısız döndü.";
                    sonuc.BitisZamani = DateTime.Now;
                    return sonuc;
                }

                sonuc.CekilenSatir = cevap.Items.Count;
                if (cevap.Items.Count == 0)
                {
                    sonuc.Basarili = true;
                    sonuc.BitisZamani = DateTime.Now;
                    return sonuc;
                }

                // 2) Hospital Name → Internal ID lookup (NAME + NORMALIZE)
                var crmHospitals = await _db.Hospitals
                    .AsNoTracking()
                    .Select(h => new { h.HospitalID, h.HospitalName })
                    .ToListAsync(ct);

                var hospitalByName = crmHospitals
                    .GroupBy(h => NormalizeName(h.HospitalName), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().HospitalID, StringComparer.OrdinalIgnoreCase);

                // 3) Test eşleşmesi — ★ YENİ ★ Leading zero normalize ile
                var testler = await _db.Tests
                    .AsNoTracking()
                    .Where(t => !string.IsNullOrEmpty(t.SutCode))
                    .Select(t => new { t.TestID, t.SutCode })
                    .ToListAsync(ct);

                var testBySutCode = testler
                    .Where(t => !string.IsNullOrWhiteSpace(t.SutCode))
                    .GroupBy(t => NormalizeRef(t.SutCode!))
                    .ToDictionary(g => g.Key, g => g.First().TestID);

                _log.LogInformation("Test lookup hazır: {Count} SutCode normalize edildi.", testBySutCode.Count);

                // 4) Eşleşmeyen hastaneleri önceden tespit
                var apiHospitalNames = cevap.Items.Select(i => i.HospitalName).Distinct().ToList();
                foreach (var apiName in apiHospitalNames)
                {
                    if (!hospitalByName.ContainsKey(NormalizeName(apiName)))
                    {
                        sonuc.EslesemeyenHastaneler.Add(apiName);
                        _log.LogWarning("Hastane eşleşmedi: '{Name}'", apiName);
                    }
                }

                // 5) Mevcut snapshot'ları yükle — upsert için
                var internalHospIds = cevap.Items
                    .Select(i => hospitalByName.TryGetValue(NormalizeName(i.HospitalName), out var id) ? (int?)id : null)
                    .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

                var mevcutlar = await _db.FizikiSayimSnapshotlari
                    .Where(s => internalHospIds.Contains(s.HospitalID))
                    .ToListAsync(ct);
                var mevcutMap = mevcutlar.ToDictionary(
                    s => BuildKey(s.HospitalID, s.ReferenceNumber, s.SayimNo),
                    s => s);

                // 6) Upsert loop
                foreach (var item in cevap.Items)
                {
                    if (!hospitalByName.TryGetValue(NormalizeName(item.HospitalName), out var internalHospitalId))
                    {
                        sonuc.AtlanilanHastaneSizKayit++;
                        continue;
                    }

                    var tipi = UrunTipiTespitHelper.Tespit(item.ProductName, item.ProductType);

                    // ★ Reaktif ise Test eşleşmesi dene — NORMALIZE EDİLMİŞ referansla
                    int? mappedTestId = null;
                    if (tipi == CRMOzem.Core.Enums.UrunTipi.Reaktif
                        && !string.IsNullOrWhiteSpace(item.ReferenceNumber))
                    {
                        var normalizedRef = NormalizeRef(item.ReferenceNumber);
                        if (testBySutCode.TryGetValue(normalizedRef, out var tid))
                        {
                            mappedTestId = tid;
                            sonuc.ReaktifEslesen++;
                        }
                        else
                        {
                            sonuc.ReaktifEslesemeyen++;
                        }
                    }

                    var key = BuildKey(internalHospitalId, item.ReferenceNumber, item.SayimNo);

                    if (mevcutMap.TryGetValue(key, out var existing))
                    {
                        // UPDATE
                        existing.ExternalProductId = item.ProductId;
                        existing.ProductName = item.ProductName;
                        existing.ProductType = item.ProductType;
                        existing.Platform = item.Platform;
                        existing.UrunTipi = tipi;
                        existing.MappedTestID = mappedTestId;
                        existing.SayimTarihi = item.SayimTarihi;
                        existing.SayimAktifMi = item.SayimAktifMi;
                        existing.BoxCount = item.BoxCount;
                        existing.FlaconCount = item.FlaconCount;
                        existing.TestCount = item.TestCount;
                        existing.BoxTextCount = item.BoxTextCount;
                        existing.FlaconCountPerBox = item.FlaconCountPerBox;
                        existing.LotNumber = item.LotNumber;
                        existing.ExpiryDate = item.ExpiryDate;
                        existing.LastUpdatedExternal = item.LastUpdated;
                        existing.CekilmeTarihi = DateTime.Now;

                        sonuc.Guncellenen++;
                    }
                    else
                    {
                        // INSERT
                        _db.FizikiSayimSnapshotlari.Add(new FizikiSayimSnapshot
                        {
                            HospitalID = internalHospitalId,
                            ExternalProductId = item.ProductId,
                            ProductName = item.ProductName,
                            ReferenceNumber = item.ReferenceNumber,
                            ProductType = item.ProductType,
                            Platform = item.Platform,
                            UrunTipi = tipi,
                            MappedTestID = mappedTestId,
                            SayimNo = item.SayimNo,
                            SayimTarihi = item.SayimTarihi,
                            SayimAktifMi = item.SayimAktifMi,
                            BoxCount = item.BoxCount,
                            FlaconCount = item.FlaconCount,
                            TestCount = item.TestCount,
                            BoxTextCount = item.BoxTextCount,
                            FlaconCountPerBox = item.FlaconCountPerBox,
                            LotNumber = item.LotNumber,
                            ExpiryDate = item.ExpiryDate,
                            LastUpdatedExternal = item.LastUpdated,
                            CekilmeTarihi = DateTime.Now
                        });
                        sonuc.YeniEklenen++;
                    }
                }

                await _db.SaveChangesAsync(ct);

                sonuc.Basarili = true;
                sonuc.BitisZamani = DateTime.Now;
                _log.LogInformation("Fiziki sayım sync bitti. {Ozet}", sonuc.Ozet);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fiziki sayım sync HATASI");
                sonuc.HataMesaji = ex.Message;
                sonuc.BitisZamani = DateTime.Now;
            }

            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEŞHİS
        // ═══════════════════════════════════════════════════════════
        public async Task<EslesmeRaporuVm> TestEslesmeAsync(CancellationToken ct = default)
        {
            var rapor = new EslesmeRaporuVm();

            var crm = await _db.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);
            rapor.CRMOzemdekiHastaneler = crm.Select(h => $"[{h.HospitalID}] {h.HospitalName}").ToList();

            var hospitalByName = crm
                .GroupBy(h => NormalizeName(h.HospitalName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new { g.First().HospitalID, g.First().HospitalName }, StringComparer.OrdinalIgnoreCase);

            var cevap = await _api.GetStocksAsync(null, null, ct);
            if (cevap == null) return rapor;

            var apiGruplar = cevap.Items
                .GroupBy(i => new { i.HospitalId, i.HospitalName })
                .Select(g => new { g.Key.HospitalId, g.Key.HospitalName, SatirSayisi = g.Count() })
                .OrderBy(g => g.HospitalName);

            foreach (var g in apiGruplar)
            {
                var normalized = NormalizeName(g.HospitalName);
                var eslesti = hospitalByName.TryGetValue(normalized, out var crmHosp);

                rapor.ApidenGelenHastaneler.Add(new EslesmeDetay
                {
                    ApiHospitalId = g.HospitalId,
                    ApiHospitalName = g.HospitalName,
                    SatirSayisi = g.SatirSayisi,
                    Eslesti = eslesti,
                    InternalHospitalId = eslesti ? crmHosp!.HospitalID : null,
                    InternalHospitalName = eslesti ? crmHosp!.HospitalName : null
                });

                if (eslesti) rapor.EslesenSayisi++;
                else rapor.EslesemeyenSayisi++;
            }

            return rapor;
        }

        // ───────────────────────────────────────────────────────────
        //  HELPERS
        // ───────────────────────────────────────────────────────────
        private static string BuildKey(int hospitalId, string referenceNumber, int sayimNo)
            => $"{hospitalId}|{NormalizeRef(referenceNumber)}|{sayimNo}";

        /// <summary>Hastane adı normalizasyonu</summary>
        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            return name
                .Trim()
                .ToLowerInvariant()
                .Replace("ı", "i")
                .Replace("i̇", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ö", "o")
                .Replace("ç", "c")
                .Replace("  ", " ");
        }

        /// <summary>
        /// ★ YENİ ★ Referans/SutCode normalizasyonu:
        /// Trim + Upper + Leading zero'ları kırp.
        /// "01E0124" → "1E0124"
        /// "0001234" → "1234"
        /// "1E0124"  → "1E0124"
        /// Boş girdi için boş string döner.
        /// </summary>
        private static string NormalizeRef(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var cleaned = s.Trim().ToUpperInvariant().TrimStart('0');
            return cleaned.Length == 0 ? "0" : cleaned;  // tamamı 0'dan ibaretse en az "0" dönsün
        }
    }
}
