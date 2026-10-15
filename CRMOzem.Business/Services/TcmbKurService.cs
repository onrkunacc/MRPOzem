using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CRMOzem.Business.Services
{
    public class TcmbKurService : ITcmbKurService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;

        // TCMB URL formatları
        // Geçmiş: https://www.tcmb.gov.tr/kurlar/YYYYMM/DDMMYYYY.xml
        // Bugün:  https://www.tcmb.gov.tr/kurlar/today.xml
        private const string TCMB_TODAY = "https://www.tcmb.gov.tr/kurlar/today.xml";
        private const string TCMB_HISTORY = "https://www.tcmb.gov.tr/kurlar/{0:yyyyMM}/{0:ddMMyyyy}.xml";

        public TcmbKurService(ApplicationDbContext db, IHttpClientFactory httpFactory)
        {
            _db = db;
            _httpFactory = httpFactory;
        }

        // ═══════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════

        public async Task<TcmbKur?> GetKurAsync(DateTime tarih, CancellationToken ct = default)
        {
            var gun = tarih.Date;

            // 1) Cache'den bak
            var cached = await _db.TcmbKurlari.AsNoTracking()
                .FirstOrDefaultAsync(k => k.Tarih == gun, ct);
            if (cached != null) return cached;

            // 2) TCMB'den çek — hafta sonu ise en yakın önceki cuma
            var trySate = gun;
            for (int i = 0; i < 10; i++)
            {
                // Hafta sonlarını atla
                while (trySate.DayOfWeek == DayOfWeek.Saturday || trySate.DayOfWeek == DayOfWeek.Sunday)
                    trySate = trySate.AddDays(-1);

                var fetched = await FetchFromTcmbAsync(trySate, ct);
                if (fetched != null)
                {
                    // Cache'e yaz — orijinal tarih ile (kullanıcının istediği tarih)
                    var entity = new TcmbKur
                    {
                        Tarih = gun,
                        UsdAlis = fetched.UsdAlis,
                        UsdSatis = fetched.UsdSatis,
                        EurAlis = fetched.EurAlis,
                        EurSatis = fetched.EurSatis,
                        UsdEfektifAlis = fetched.UsdEfektifAlis,
                        UsdEfektifSatis = fetched.UsdEfektifSatis,
                        CekilmeZamani = DateTime.Now,
                        ManuelGirildi = false,
                        Not = trySate != gun ? $"TCMB verisi {trySate:dd.MM.yyyy} tarihinden alındı" : null
                    };
                    _db.TcmbKurlari.Add(entity);
                    await _db.SaveChangesAsync(ct);
                    return entity;
                }

                // O gün de yoksa bir gün geriye git
                trySate = trySate.AddDays(-1);
            }

            return null;
        }

        public Task<TcmbKur?> GetGuncelKurAsync(CancellationToken ct = default)
            => GetKurAsync(DateTime.Today, ct);

        public async Task<(decimal? tlKarsiligi, decimal? kur, DateTime? kurTarihi)> UsdToTryAsync(
            decimal usdTutar, DateTime tarih, CancellationToken ct = default)
        {
            var kur = await GetKurAsync(tarih, ct);
            if (kur == null) return (null, null, null);
            var tl = Math.Round(usdTutar * kur.UsdSatis, 2);
            return (tl, kur.UsdSatis, kur.Tarih);
        }

        public async Task<(decimal? usdKarsiligi, decimal? kur, DateTime? kurTarihi)> TryToUsdAsync(
            decimal tlTutar, DateTime tarih, CancellationToken ct = default)
        {
            var kur = await GetKurAsync(tarih, ct);
            if (kur == null || kur.UsdSatis <= 0) return (null, null, null);
            var usd = Math.Round(tlTutar / kur.UsdSatis, 2);
            return (usd, kur.UsdSatis, kur.Tarih);
        }

        public async Task<List<TcmbKur>> GetKurListesiAsync(DateTime baslangic, DateTime bitis, CancellationToken ct = default)
        {
            var b = baslangic.Date;
            var e = bitis.Date;
            return await _db.TcmbKurlari.AsNoTracking()
                .Where(k => k.Tarih >= b && k.Tarih <= e)
                .OrderByDescending(k => k.Tarih)
                .ToListAsync(ct);
        }

        public async Task<TcmbKur> ManuelKurEkleAsync(DateTime tarih, decimal usdAlis, decimal usdSatis, string? not, CancellationToken ct = default)
        {
            var gun = tarih.Date;
            var existing = await _db.TcmbKurlari.FirstOrDefaultAsync(k => k.Tarih == gun, ct);

            if (existing != null)
            {
                existing.UsdAlis = usdAlis;
                existing.UsdSatis = usdSatis;
                existing.ManuelGirildi = true;
                existing.Not = not;
                existing.CekilmeZamani = DateTime.Now;
            }
            else
            {
                existing = new TcmbKur
                {
                    Tarih = gun,
                    UsdAlis = usdAlis,
                    UsdSatis = usdSatis,
                    ManuelGirildi = true,
                    Not = not,
                    CekilmeZamani = DateTime.Now
                };
                _db.TcmbKurlari.Add(existing);
            }

            await _db.SaveChangesAsync(ct);
            return existing;
        }

        public async Task<int> BulkFetchAsync(DateTime baslangic, DateTime bitis, CancellationToken ct = default)
        {
            int eklenen = 0;
            var mevcut = await _db.TcmbKurlari.AsNoTracking()
                .Where(k => k.Tarih >= baslangic.Date && k.Tarih <= bitis.Date)
                .Select(k => k.Tarih)
                .ToListAsync(ct);
            var mevcutSet = new HashSet<DateTime>(mevcut);

            var current = baslangic.Date;
            while (current <= bitis.Date)
            {
                if (!mevcutSet.Contains(current) &&
                    current.DayOfWeek != DayOfWeek.Saturday &&
                    current.DayOfWeek != DayOfWeek.Sunday)
                {
                    var fetched = await FetchFromTcmbAsync(current, ct);
                    if (fetched != null)
                    {
                        _db.TcmbKurlari.Add(new TcmbKur
                        {
                            Tarih = current,
                            UsdAlis = fetched.UsdAlis,
                            UsdSatis = fetched.UsdSatis,
                            EurAlis = fetched.EurAlis,
                            EurSatis = fetched.EurSatis,
                            UsdEfektifAlis = fetched.UsdEfektifAlis,
                            UsdEfektifSatis = fetched.UsdEfektifSatis,
                            CekilmeZamani = DateTime.Now
                        });
                        eklenen++;

                        // Her 20 kayıtta bir kaydet
                        if (eklenen % 20 == 0)
                            await _db.SaveChangesAsync(ct);
                    }

                    // TCMB'yi bombalamayalım
                    await Task.Delay(100, ct);
                }
                current = current.AddDays(1);
            }

            if (eklenen > 0)
                await _db.SaveChangesAsync(ct);

            return eklenen;
        }

        // ═══════════════════════════════════════════════════════════
        // Private — TCMB XML parse
        // ═══════════════════════════════════════════════════════════
        private async Task<TcmbKur?> FetchFromTcmbAsync(DateTime tarih, CancellationToken ct)
        {
            try
            {
                var url = tarih.Date == DateTime.Today
                    ? TCMB_TODAY
                    : string.Format(CultureInfo.InvariantCulture, TCMB_HISTORY, tarih);

                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(10);

                var response = await http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) return null;

                var xml = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(xml)) return null;

                var doc = XDocument.Parse(xml);
                var currencies = doc.Root?.Elements("Currency");
                if (currencies == null) return null;

                var usd = currencies.FirstOrDefault(c => (string?)c.Attribute("Kod") == "USD");
                var eur = currencies.FirstOrDefault(c => (string?)c.Attribute("Kod") == "EUR");
                if (usd == null) return null;

                decimal ParseDecimal(XElement? parent, string el)
                {
                    var v = parent?.Element(el)?.Value;
                    if (string.IsNullOrWhiteSpace(v)) return 0;
                    return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
                }

                return new TcmbKur
                {
                    UsdAlis = ParseDecimal(usd, "ForexBuying"),
                    UsdSatis = ParseDecimal(usd, "ForexSelling"),
                    UsdEfektifAlis = ParseDecimal(usd, "BanknoteBuying"),
                    UsdEfektifSatis = ParseDecimal(usd, "BanknoteSelling"),
                    EurAlis = eur != null ? ParseDecimal(eur, "ForexBuying") : null,
                    EurSatis = eur != null ? ParseDecimal(eur, "ForexSelling") : null
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
