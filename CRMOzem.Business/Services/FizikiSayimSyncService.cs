using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class FizikiSayimSyncService : IFizikiSayimSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly IFizikiSayimApiClient _api;
        private readonly ILogger<FizikiSayimSyncService> _log;


        private static readonly DateTime SqlMinTarih = new DateTime(1753, 1, 1);

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

                
                var crmHospitals = await _db.Hospitals
                    .AsNoTracking()
                    .Select(h => new { h.HospitalID, h.HospitalName })
                    .ToListAsync(ct);

                var hospitalByName = crmHospitals
                    .GroupBy(h => NormalizeName(h.HospitalName), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().HospitalID, StringComparer.OrdinalIgnoreCase);

                var manuelMappings = await _db.FizikiSayimHastaneEslesmeleri
                    .AsNoTracking()
                    .ToListAsync(ct);

                var yoksayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in manuelMappings)
                {
                    var key = NormalizeName(m.ApiHospitalName);
                    if (string.IsNullOrEmpty(key)) continue;

                    if (m.Yoksay)
                        yoksayNames.Add(key);
                    else if (m.InternalHospitalID.HasValue)
                        hospitalByName[key] = m.InternalHospitalID.Value;  
                }

                _log.LogInformation("Manuel eşleştirme: {Toplam} kayıt ({Override} override, {Yoksay} yoksay)",
                    manuelMappings.Count,
                    manuelMappings.Count(m => !m.Yoksay && m.InternalHospitalID.HasValue),
                    yoksayNames.Count);

               
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

               
                var apiHospitalNames = cevap.Items.Select(i => i.HospitalName).Distinct().ToList();
                foreach (var apiName in apiHospitalNames)
                {
                    var normalized = NormalizeName(apiName);
                    if (yoksayNames.Contains(normalized)) continue;
                    if (!hospitalByName.ContainsKey(normalized))
                    {
                        sonuc.EslesemeyenHastaneler.Add(apiName);
                        _log.LogWarning("Hastane eşleşmedi: '{Name}'", apiName);
                    }
                }

                
                var internalHospIds = cevap.Items
                    .Select(i => hospitalByName.TryGetValue(NormalizeName(i.HospitalName), out var id) ? (int?)id : null)
                    .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

                var mevcutlar = await _db.FizikiSayimSnapshotlari
                    .Where(s => internalHospIds.Contains(s.HospitalID))
                    .ToListAsync(ct);

                var mevcutMap = mevcutlar
                    .GroupBy(s => BuildKey(s.HospitalID, s.ReferenceNumber, s.SayimNo, s.LotNumber))
                    .ToDictionary(g => g.Key, g => g.First());

                var duplicateler = mevcutlar
                    .GroupBy(s => BuildKey(s.HospitalID, s.ReferenceNumber, s.SayimNo, s.LotNumber))
                    .Where(g => g.Count() > 1)
                    .Take(10)
                    .ToList();
                if (duplicateler.Any())
                {
                    _log.LogWarning("Duplicate snapshot tespit edildi ({N} key): {Keys}",
                        duplicateler.Count,
                        string.Join(", ", duplicateler.Select(g => $"{g.Key} ×{g.Count()}")));
                }

     
                var batchTekrari = 0;

       
                foreach (var item in cevap.Items)
                {
                    var normalized = NormalizeName(item.HospitalName);

                    if (yoksayNames.Contains(normalized))
                    {
                        sonuc.AtlanilanHastaneSizKayit++;
                        continue;
                    }

                    if (!hospitalByName.TryGetValue(normalized, out var internalHospitalId))
                    {
                        sonuc.AtlanilanHastaneSizKayit++;
                        continue;
                    }

                    var tipi = UrunTipiTespitHelper.Tespit(item.ProductName, item.ProductType);

                    
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

                    var key = BuildKey(internalHospitalId, item.ReferenceNumber, item.SayimNo, item.LotNumber);

                    if (mevcutMap.TryGetValue(key, out var existing))
                    {
                       
                        if (_db.Entry(existing).State == EntityState.Added)
                            batchTekrari++;
                        else
                            sonuc.Guncellenen++;

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
                    }
                    else
                    {
                        var yeni = new FizikiSayimSnapshot
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
                        };

                        _db.FizikiSayimSnapshotlari.Add(yeni);

                      
                        mevcutMap[key] = yeni;

                        sonuc.YeniEklenen++;
                    }
                }

                if (batchTekrari > 0)
                {
                    _log.LogWarning(
                        "API aynı batch içinde {N} tekrar eden satır döndü (aynı hastane+referans+sayımNo+lot). " +
                        "Son gelen değer kullanıldı, duplicate INSERT engellendi.", batchTekrari);
                }

              
                var duzeltilen = SnapshotlariTemizle();
                if (duzeltilen > 0)
                    _log.LogWarning("Kaydetmeden önce {N} alan otomatik düzeltildi (kırpma/tarih).", duzeltilen);

                await _db.SaveChangesAsync(ct);

                sonuc.Basarili = true;
                sonuc.BitisZamani = DateTime.Now;
                _log.LogInformation("Fiziki sayım sync bitti. {Ozet}", sonuc.Ozet);
            }
            catch (DbUpdateException ex)
            {
               
                var zincir = HataZinciri(ex);
                var kayitlar = SorunluKayitlar(ex);

                _log.LogError(ex,
                    "Fiziki sayım sync — VERİTABANI HATASI\nZincir: {Zincir}\nEtkilenen kayıtlar:\n{Kayitlar}",
                    zincir, kayitlar);

                sonuc.HataMesaji = EnIcMesaj(ex) + "  |  Detay için uygulama log'una bakın.";
                sonuc.BitisZamani = DateTime.Now;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fiziki sayım sync HATASI. Zincir: {Zincir}", HataZinciri(ex));

                sonuc.HataMesaji = EnIcMesaj(ex);
                sonuc.BitisZamani = DateTime.Now;
            }

            return sonuc;
        }


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

          
            var manuelMappings = await _db.FizikiSayimHastaneEslesmeleri
                .AsNoTracking()
                .Include(e => e.InternalHospital)
                .ToListAsync(ct);

       
            var manuelByName = manuelMappings
                .GroupBy(m => NormalizeName(m.ApiHospitalName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var cevap = await _api.GetStocksAsync(null, null, ct);
            if (cevap == null) return rapor;

            var apiGruplar = cevap.Items
                .GroupBy(i => new { i.HospitalId, i.HospitalName })
                .Select(g => new { g.Key.HospitalId, g.Key.HospitalName, SatirSayisi = g.Count() })
                .OrderBy(g => g.HospitalName);

            foreach (var g in apiGruplar)
            {
                var normalized = NormalizeName(g.HospitalName);
                var detay = new EslesmeDetay
                {
                    ApiHospitalId = g.HospitalId,
                    ApiHospitalName = g.HospitalName,
                    SatirSayisi = g.SatirSayisi
                };

                if (manuelByName.TryGetValue(normalized, out var manuel))
                {
                    if (manuel.Yoksay)
                    {
                        detay.Eslesti = false;
                        detay.ManuelYoksay = true;
                    }
                    else if (manuel.InternalHospitalID.HasValue)
                    {
                        detay.Eslesti = true;
                        detay.ManuelEslestirildi = true;
                        detay.InternalHospitalId = manuel.InternalHospitalID;
                        detay.InternalHospitalName = manuel.InternalHospital?.HospitalName;
                    }
                }


                if (!detay.Eslesti && !detay.ManuelYoksay && hospitalByName.TryGetValue(normalized, out var crmHosp))
                {
                    detay.Eslesti = true;
                    detay.InternalHospitalId = crmHosp.HospitalID;
                    detay.InternalHospitalName = crmHosp.HospitalName;
                }

                rapor.ApidenGelenHastaneler.Add(detay);

                if (detay.Eslesti) rapor.EslesenSayisi++;
                else rapor.EslesemeyenSayisi++;
            }

            return rapor;
        }

     
        private int SnapshotlariTemizle()
        {
            var entityType = _db.Model.FindEntityType(typeof(FizikiSayimSnapshot));
            if (entityType == null) return 0;

            var props = entityType.GetProperties()
                .Where(p => !p.IsPrimaryKey())
                .ToList();

            var duzeltme = 0;

            foreach (var entry in _db.ChangeTracker.Entries<FizikiSayimSnapshot>().ToList())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                foreach (var prop in props)
                {
                    var pe = entry.Property(prop.Name);
                    var deger = pe.CurrentValue;
                    if (deger == null) continue;

                    if (deger is string metin)
                    {
                        var max = prop.GetMaxLength();
                        if (max.HasValue && max.Value > 0 && metin.Length > max.Value)
                        {
                            pe.CurrentValue = metin.Substring(0, max.Value);
                            duzeltme++;

                            _log.LogWarning(
                                "Kırpıldı · {Prop}: {Uzunluk} → {Max} karakter · HospitalID={Hid} Ref='{Ref}' · Değer: '{Deger}'",
                                prop.Name, metin.Length, max.Value,
                                entry.Entity.HospitalID, entry.Entity.ReferenceNumber,
                                metin.Length > 80 ? metin.Substring(0, 80) + "…" : metin);
                        }
                    }
          
                    else if (deger is DateTime tarih && tarih < SqlMinTarih)
                    {
                        var nullableMi = Nullable.GetUnderlyingType(prop.ClrType) != null;
                        pe.CurrentValue = nullableMi ? null : (object)SqlMinTarih;
                        duzeltme++;

                        _log.LogWarning(
                            "Geçersiz tarih düzeltildi · {Prop} = {Deger:yyyy-MM-dd} · HospitalID={Hid} Ref='{Ref}'",
                            prop.Name, tarih, entry.Entity.HospitalID, entry.Entity.ReferenceNumber);
                    }
                }
            }

            return duzeltme;
        }

  
        private static string HataZinciri(Exception ex)
        {
            var sb = new StringBuilder();
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (sb.Length > 0) sb.Append("  →  ");
                sb.Append(e.GetType().Name).Append(": ").Append(e.Message);
            }
            return sb.ToString();
        }

        private static string EnIcMesaj(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }

        private static string SorunluKayitlar(DbUpdateException ex, int limit = 5)
        {
            if (ex.Entries == null || ex.Entries.Count == 0)
                return "(EF hangi kaydın hata verdiğini bildirmedi)";

            var sb = new StringBuilder();

            foreach (var entry in ex.Entries.Take(limit))
            {
                sb.AppendLine($"- {entry.Entity.GetType().Name} [{entry.State}]");

                if (entry.Entity is FizikiSayimSnapshot s)
                {
                    sb.AppendLine($"    HospitalID={s.HospitalID}  SayimNo={s.SayimNo}  Ref='{s.ReferenceNumber}'  Lot='{s.LotNumber}'");
                    sb.AppendLine($"    ProductName  ({Uzunluk(s.ProductName)}) = '{s.ProductName}'");
                    sb.AppendLine($"    ProductType  ({Uzunluk(s.ProductType)}) = '{s.ProductType}'");
                    sb.AppendLine($"    Platform     ({Uzunluk(s.Platform)}) = '{s.Platform}'");
                    sb.AppendLine($"    SayimTarihi={s.SayimTarihi:yyyy-MM-dd}  ExpiryDate={s.ExpiryDate:yyyy-MM-dd}  LastUpdatedExternal={s.LastUpdatedExternal:yyyy-MM-dd}");
                }
            }

            if (ex.Entries.Count > limit)
                sb.AppendLine($"  … +{ex.Entries.Count - limit} kayıt daha");

            return sb.ToString().TrimEnd();
        }

        private static int Uzunluk(string? s) => s?.Length ?? 0;

       
        private static string BuildKey(int hospitalId, string? referenceNumber, int sayimNo, string? lotNumber = null)
            => $"{hospitalId}|{NormalizeRef(referenceNumber)}|{sayimNo}|{(lotNumber ?? "").Trim().ToUpperInvariant()}";

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

        private static string NormalizeRef(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var cleaned = s.Trim().ToUpperInvariant().TrimStart('0');
            return cleaned.Length == 0 ? "0" : cleaned;
        }
    }
}