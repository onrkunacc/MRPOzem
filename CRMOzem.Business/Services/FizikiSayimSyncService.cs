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
                    sonuc.HataMesaji = "Sayım uygulaması (crm.ozemmedikal.com) cevap vermedi. "
                        + "Adres ve API anahtarını (appsettings → StokSayimApi) ve sunucunun açık olduğunu kontrol edin; "
                        + "ayrıntı uygulama log'unda.";
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


                // Katalog (Testler) — referans, referans kökü ve ada göre aranır.
                // SutCode'u boş testler de alınır: ada göre eşleşebilirler.
                var testler = await _db.Tests
                    .AsNoTracking()
                    .Select(t => new KatalogSatiri
                    {
                        TestID = t.TestID,
                        TestName = t.TestName,
                        SutCode = t.SutCode,
                        Platform = t.Platform
                    })
                    .ToListAsync(ct);

                var katalog = new KatalogEslestirici(testler);

                _log.LogInformation("Katalog hazır: {Count} test.", testler.Count);


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


                // ── Katalogda bulunamayan reaktifleri Testler'e ekle ──
                // Üç aşamalı arama (referans → referans kökü → ad) sonuç vermeyen
                // reaktifler stok hesabına hiç girmiyordu. Bunlar kataloğa
                // "Sayımdan eklendi" kategorisiyle eklenir; ad, referans, platform
                // ve kutu içi adet sayım uygulamasındaki değerlerdir.
                // Yalnızca REAKTİF ürünler ve yalnızca referansı olanlar eklenir.
                var eklenecek = new Dictionary<string, Test>();
                foreach (var item in cevap.Items)
                {
                    var hAd = NormalizeName(item.HospitalName);
                    if (yoksayNames.Contains(hAd) || !hospitalByName.ContainsKey(hAd)) continue;

                    if (UrunTipiTespitHelper.Tespit(item.ProductName, item.ProductType) != CRMOzem.Core.Enums.UrunTipi.Reaktif)
                        continue;

                    var refNorm = ReferansKodu.Normalize(item.ReferenceNumber);
                    if (refNorm.Length == 0 || string.IsNullOrWhiteSpace(item.ProductName)) continue;
                    if (eklenecek.ContainsKey(refNorm)) continue;

                    var (bulunan, _) = katalog.Bul(item.ReferenceNumber, item.ProductName, item.Platform, reaktif: true);
                    if (bulunan.HasValue) continue;

                    eklenecek[refNorm] = new Test
                    {
                        TestName = Kisalt(item.ProductName.Trim(), 100)!,
                        SutCode = Kisalt(item.ReferenceNumber!.Trim(), 30),
                        Platform = Kisalt(item.Platform?.Trim(), 50),
                        Kategori = "Sayımdan eklendi",
                        GirisTipi = item.BoxTextCount > 1 ? TestGirisTipi.Kutu : TestGirisTipi.Adet,
                        KutuBasinaAdet = item.BoxTextCount > 1 ? item.BoxTextCount : null
                    };
                }

                if (eklenecek.Count > 0)
                {
                    _db.Tests.AddRange(eklenecek.Values);
                    await _db.SaveChangesAsync(ct);   // TestID'ler oluşsun

                    foreach (var t in eklenecek.Values)
                    {
                        katalog.Ekle(new KatalogSatiri
                        {
                            TestID = t.TestID,
                            TestName = t.TestName,
                            SutCode = t.SutCode,
                            Platform = t.Platform
                        });
                        sonuc.EklenenTestler.Add($"{t.TestName} ({t.SutCode})");
                    }

                    _log.LogInformation("Kataloğa {N} reaktif eklendi: {Liste}",
                        eklenecek.Count, string.Join(", ", sonuc.EklenenTestler));
                }

                var kokIle = new HashSet<string>();
                var adIle = new HashSet<string>();

                var batchTekrari = 0;
                var aktifSayimlar = new Dictionary<int, HashSet<int>>();

                // Sayım uygulamasındaki İKİ ayrı hastane bizde TEK hastaneye bağlı
                // olabilir. İkisinde de aynı ürün + sayım no + lot varsa satırlar
                // aynı kayda düşer; biri diğerini EZMEMELİ, miktarlar TOPLANMALI.
                // Bu turda her kaydı ilk yazan sayım-hastanesi burada tutulur.
                var kayitKaynagi = new Dictionary<string, int>();
                var birlesenHastaneler = new Dictionary<int, HashSet<string>>();


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


                    // Referans numarası katalogda varsa ürün tipi ne tespit edilmiş
                    // olursa olsun eşleştir. (Eskiden yalnızca "reaktif" tespit edilen
                    // ürünler eşleştiriliyor, tire/nokta farkı da eşleşmeyi bozuyordu.)
                    var reaktifMi = tipi == CRMOzem.Core.Enums.UrunTipi.Reaktif;
                    var (mappedTestId, yontem) = katalog.Bul(item.ReferenceNumber, item.ProductName, item.Platform, reaktifMi);

                    if (mappedTestId.HasValue)
                    {
                        if (reaktifMi) sonuc.ReaktifEslesen++;

                        var urunAnahtar = $"{item.ProductName} ({item.ReferenceNumber})";
                        if (yontem == KatalogEslesmeYontemi.ReferansKoku) kokIle.Add(urunAnahtar);
                        else if (yontem == KatalogEslesmeYontemi.Ad) adIle.Add(urunAnahtar);
                    }
                    else if (reaktifMi)
                    {
                        sonuc.ReaktifEslesemeyen++;
                    }

                    // Hastanenin API'de "aktif" görünen sayım numaraları
                    if (item.SayimAktifMi)
                    {
                        if (!aktifSayimlar.TryGetValue(internalHospitalId, out var nolar))
                            aktifSayimlar[internalHospitalId] = nolar = new HashSet<int>();
                        nolar.Add(item.SayimNo);
                    }

                    var key = BuildKey(internalHospitalId, item.ReferenceNumber, item.SayimNo, item.LotNumber);

                    if (!birlesenHastaneler.TryGetValue(internalHospitalId, out var kaynakAdlari))
                        birlesenHastaneler[internalHospitalId] = kaynakAdlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    kaynakAdlari.Add(item.HospitalName ?? "");

                    // Aynı kayda bu turda BAŞKA bir sayım-hastanesinden satır geldiyse topla
                    if (kayitKaynagi.TryGetValue(key, out var ilkKaynak) && ilkKaynak != item.HospitalId
                        && mevcutMap.TryGetValue(key, out var ortak))
                    {
                        ortak.BoxCount += item.BoxCount;
                        ortak.FlaconCount += item.FlaconCount;
                        ortak.TestCount += item.TestCount;
                        if (item.SayimTarihi > ortak.SayimTarihi) ortak.SayimTarihi = item.SayimTarihi;
                        ortak.SayimAktifMi = ortak.SayimAktifMi || item.SayimAktifMi;
                        sonuc.BirlestirilenSatir++;
                        continue;
                    }
                    kayitKaynagi[key] = item.HospitalId;

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

                sonuc.BirlesenHastaneSayisi = birlesenHastaneler.Values.Count(v => v.Count > 1);
                sonuc.ReferansKokuyleEslesen = kokIle.OrderBy(x => x).ToList();
                sonuc.AdlaEslesen = adIle.OrderBy(x => x).ToList();

                if (batchTekrari > 0)
                {
                    _log.LogWarning(
                        "API aynı batch içinde {N} tekrar eden satır döndü (aynı hastane+referans+sayımNo+lot). " +
                        "Son gelen değer kullanıldı, duplicate INSERT engellendi.", batchTekrari);
                }


                // ── Yerine yenisi gelen sayımları kapat ──
                // Sayım uygulaması yeni sayım açınca eskisini artık göndermeyebilir;
                // bizdeki eski satırlar "aktif" kalırsa yeni sayımla TOPLANIP stoğu
                // şişirir. Hastanenin API'de aktif bir sayımı varsa, bizde aktif
                // görünen diğer sayım numaraları kapatılır.
                foreach (var m in mevcutlar)
                {
                    if (!m.SayimAktifMi) continue;
                    if (!aktifSayimlar.TryGetValue(m.HospitalID, out var nolar)) continue;
                    if (nolar.Contains(m.SayimNo)) continue;

                    m.SayimAktifMi = false;
                    sonuc.KapatilanEskiSatir++;
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
            rapor.ApiyeUlasildi = true;

            var apiGruplar = cevap.Items
                .GroupBy(i => new { i.HospitalId, i.HospitalName })
                .Select(g => new
                {
                    g.Key.HospitalId,
                    g.Key.HospitalName,
                    SatirSayisi = g.Count(),
                    SonSayim = g.Max(x => x.SayimTarihi)
                })
                .OrderBy(g => g.HospitalName);

            var bizdekiHam = await _db.FizikiSayimSnapshotlari.AsNoTracking()
                .Where(s => s.SayimAktifMi)
                .GroupBy(s => s.HospitalID)
                .Select(g => new { HospitalID = g.Key, Son = g.Max(x => x.SayimTarihi) })
                .ToListAsync(ct);
            var bizdeki = bizdekiHam.ToDictionary(x => x.HospitalID, x => x.Son);

            foreach (var g in apiGruplar)
            {
                var normalized = NormalizeName(g.HospitalName);
                var detay = new EslesmeDetay
                {
                    ApiHospitalId = g.HospitalId,
                    ApiHospitalName = g.HospitalName,
                    SatirSayisi = g.SatirSayisi,
                    ApiSonSayim = g.SonSayim
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

                if (detay.InternalHospitalId.HasValue && bizdeki.TryGetValue(detay.InternalHospitalId.Value, out var bs))
                    detay.BizdekiSonSayim = bs;

                rapor.ApidenGelenHastaneler.Add(detay);

                if (detay.Eslesti) rapor.EslesenSayisi++;
                else rapor.EslesemeyenSayisi++;
            }

            // Aynı hastanemize bağlı birden çok sayım-hastanesi: birbirlerini göstersinler
            foreach (var grup in rapor.ApidenGelenHastaneler
                         .Where(d => d.Eslesti && d.InternalHospitalId.HasValue)
                         .GroupBy(d => d.InternalHospitalId!.Value)
                         .Where(g => g.Count() > 1))
            {
                foreach (var d in grup)
                    d.BirlikteSayilanlar = grup.Where(x => x != d).Select(x => x.ApiHospitalName).ToList();
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

        private static string? Kisalt(string? s, int uzunluk) =>
                    string.IsNullOrEmpty(s) || s.Length <= uzunluk ? s : s.Substring(0, uzunluk);

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