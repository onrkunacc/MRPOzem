using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CRMOzem.Business.Services
{
    public class TcmbKurSaglayici : IKurSaglayici
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TcmbKurSaglayici> _log;

        /// <summary>Bülten bulunamazsa geriye doğru kaç gün denensin.</summary>
        private const int MaksGeriGun = 10;

        private const string CacheOnEki = "tcmb_usdtry_";

        public TcmbKurSaglayici(HttpClient http, IMemoryCache cache, ILogger<TcmbKurSaglayici> log)
        {
            _http = http;
            _cache = cache;
            _log = log;

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri("https://www.tcmb.gov.tr/");

            if (_http.Timeout == TimeSpan.FromSeconds(100)) // varsayılansa kısalt
                _http.Timeout = TimeSpan.FromSeconds(20);
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK TARİH
        // ═══════════════════════════════════════════════════════════

        public async Task<KurSonucu> GetUsdTryAsync(DateTime tarih, CancellationToken ct = default)
        {
            var istenen = tarih.Date;

            // Gelecek tarih istenirse bugünden geriye bak
            var baslangic = istenen > DateTime.Today ? DateTime.Today : istenen;

            for (var gun = 0; gun <= MaksGeriGun; gun++)
            {
                ct.ThrowIfCancellationRequested();

                var deneme = baslangic.AddDays(-gun);

                // Hafta sonu bülten yok — boşuna istek atma
                if (deneme.DayOfWeek == DayOfWeek.Saturday || deneme.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                var kur = await GunlukKurAsync(deneme, ct);
                if (kur.HasValue)
                {
                    return new KurSonucu
                    {
                        Bulundu = true,
                        Kur = kur.Value,
                        KurTarihi = deneme,
                        IstenenTarih = istenen,
                        Kaynak = "TCMB"
                    };
                }
            }

            _log.LogWarning("TCMB kuru bulunamadı. İstenen tarih: {Tarih:yyyy-MM-dd} ({Gun} gün geriye bakıldı)",
                istenen, MaksGeriGun);

            return new KurSonucu
            {
                Bulundu = false,
                IstenenTarih = istenen,
                Kaynak = "TCMB",
                Hata = $"{istenen:dd.MM.yyyy} ve öncesindeki {MaksGeriGun} gün için TCMB bülteni alınamadı."
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  TOPLU
        // ═══════════════════════════════════════════════════════════

        public async Task<Dictionary<DateTime, KurSonucu>> GetUsdTryAsync(
            IEnumerable<DateTime> tarihler,
            CancellationToken ct = default)
        {
            var benzersiz = tarihler
                .Select(t => t.Date)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var sonuc = new Dictionary<DateTime, KurSonucu>();

            foreach (var t in benzersiz)
            {
                ct.ThrowIfCancellationRequested();
                sonuc[t] = await GetUsdTryAsync(t, ct);
            }

            var bulunan = sonuc.Values.Count(v => v.Bulundu);
            _log.LogInformation("TCMB kur çözümü: {Bulunan}/{Toplam} tarih.", bulunan, benzersiz.Count);

            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK GÜNLÜK BÜLTEN
        // ═══════════════════════════════════════════════════════════

        /// <summary>Verilen güne ait bülteni getirir. Bülten yoksa null döner (geriye gitmez).</summary>
        private async Task<decimal?> GunlukKurAsync(DateTime gun, CancellationToken ct)
        {
            var anahtar = CacheOnEki + gun.ToString("yyyyMMdd");

            if (_cache.TryGetValue<decimal?>(anahtar, out var onbellek))
                return onbellek;

            decimal? kur = null;

            try
            {
                // https://www.tcmb.gov.tr/kurlar/202611/04112026.xml
                var url = $"kurlar/{gun:yyyyMM}/{gun:ddMMyyyy}.xml";

                using var cevap = await _http.GetAsync(url, ct);

                if (cevap.StatusCode == HttpStatusCode.NotFound)
                {
                    // O gün bülten yok — tatil ya da henüz yayımlanmamış
                    kur = null;
                }
                else if (!cevap.IsSuccessStatusCode)
                {
                    _log.LogWarning("TCMB {Tarih:yyyy-MM-dd} isteği {Status} döndü.", gun, (int)cevap.StatusCode);
                    kur = null;
                }
                else
                {
                    var icerik = await cevap.Content.ReadAsStringAsync(ct);
                    kur = UsdAyikla(icerik);

                    if (kur == null)
                        _log.LogWarning("TCMB {Tarih:yyyy-MM-dd} bülteni alındı ama USD satırı okunamadı.", gun);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "TCMB {Tarih:yyyy-MM-dd} bülteni alınamadı.", gun);
                kur = null;
            }

            // Geçmiş günün bülteni değişmez — uzun süre sakla.
            // Bugün/bulunamadı durumlarında kısa tut ki sonra tekrar denensin.
            var sure = (kur.HasValue && gun < DateTime.Today)
                ? TimeSpan.FromDays(30)
                : TimeSpan.FromMinutes(20);

            _cache.Set(anahtar, kur, sure);

            return kur;
        }

        /// <summary>
        /// TCMB XML'inden USD döviz satış kurunu çıkarır.
        /// Beklenen yapı: Tarih_Date → Currency[CurrencyCode="USD"] → ForexSelling
        /// Satış yoksa alış, o da yoksa efektif satış kullanılır.
        /// </summary>
        internal static decimal? UsdAyikla(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;

            try
            {
                var doc = XDocument.Parse(xml);

                var usd = doc.Descendants("Currency").FirstOrDefault(c =>
                    string.Equals((string?)c.Attribute("CurrencyCode"), "USD", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((string?)c.Attribute("Kod"), "USD", StringComparison.OrdinalIgnoreCase));

                if (usd == null) return null;

                var deger = SayiOku(usd.Element("ForexSelling")?.Value)
                         ?? SayiOku(usd.Element("ForexBuying")?.Value)
                         ?? SayiOku(usd.Element("BanknoteSelling")?.Value)
                         ?? SayiOku(usd.Element("BanknoteBuying")?.Value);

                if (deger == null || deger.Value <= 0) return null;

                // Unit normalde 1'dir; bazı para birimlerinde 100'dür.
                var birim = SayiOku(usd.Element("Unit")?.Value) ?? 1m;
                if (birim <= 0) birim = 1m;

                return Math.Round(deger.Value / birim, 6);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>TCMB sayıları "34,2345" biçiminde — virgül ondalık ayracıdır.</summary>
        private static decimal? SayiOku(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var temiz = s.Trim().Replace(" ", "");

            if (decimal.TryParse(temiz.Replace(',', '.'), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var sonuc))
                return sonuc;

            if (decimal.TryParse(temiz, NumberStyles.Any,
                    CultureInfo.GetCultureInfo("tr-TR"), out sonuc))
                return sonuc;

            return null;
        }
    }
}
