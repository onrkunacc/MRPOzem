using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels.Admin;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        public AdminDashboardService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
        }

        public async Task<AdminDashboardVm> GetDashboardAsync(string periyot, CancellationToken ct = default)
        {
            var cacheKey = $"admin_dashboard_{periyot ?? "ay"}";

            if (_cache.TryGetValue(cacheKey, out AdminDashboardVm? cached) && cached != null)
                return cached;

            var (rangeStart, rangeEnd) = GetPeriyotRange(periyot);

            var ihaleAggregates = await GetIhaleAggregatesAsync(ct);

            var vm = new AdminDashboardVm
            {
                Kpi = await BuildKpiAsync(ihaleAggregates, ct),
                MasrafAnaliz = await BuildMasrafAnalizAsync(rangeStart, rangeEnd, periyot, ct),
                AylikTrend = await BuildAylikTrendAsync(ct),
                AktifIhaleler = BuildAktifIhalelerFromAggregates(ihaleAggregates),
                HastaneBirimOzet = await BuildHastaneBirimOzetAsync(ct),
                BekleyenOnaylar = await BuildBekleyenOnaylarAsync(ct)
            };

            _cache.Set(cacheKey, vm, CacheDuration);
            return vm;
        }

        // ──────────────────────────────────────────────────────────────
        // İHALE AGREGELERİ — Computed property'ler için bellekte hesap
        // ──────────────────────────────────────────────────────────────
        private async Task<List<IhaleAggregate>> GetIhaleAggregatesAsync(CancellationToken ct)
        {
            // 1) Aktif ihalelerin header bilgisi
            var ihaleler = await _context.Ihaleler.AsNoTracking()
                .Where(i => i.Aktif)
                .OrderByDescending(i => i.OlusturmaTarihi)
                .Select(i => new
                {
                    i.Id,
                    i.IhaleAdi,
                    i.IhaleKayitNo,
                    i.IhaleTarihi,
                    i.IhaleSuresiGun,
                    i.GenelGiderOran,
                    KalemSayisi = i.IhaleKalemler.Count()
                })
                .ToListAsync(ct);

            if (ihaleler.Count == 0)
                return new List<IhaleAggregate>();

            var ihaleIds = ihaleler.Select(i => i.Id).ToList();

            // 2) Personel — tüm satırları belleğe çek, bellekte grupla
            //    (ToplamTRY computed property olduğu için SQL'de Sum yapamıyoruz)
            var personeller = await _context.PersonelGiderler.AsNoTracking()
                .Where(p => ihaleIds.Contains(p.IhaleId))
                .ToListAsync(ct);

            var personelToplamlar = personeller
                .GroupBy(p => p.IhaleId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ToplamTRY));

            // 3) Ek gider — aynı şekilde
            var ekGiderler = await _context.EkGiderler.AsNoTracking()
                .Where(e => ihaleIds.Contains(e.IhaleId))
                .ToListAsync(ct);

            var ekGiderToplamlar = ekGiderler
                .GroupBy(e => e.IhaleId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TutarTRY));

            // 4) Kalemler — aynı şekilde
            var kalemler = await _context.IhaleKalemler.AsNoTracking()
                .Where(k => ihaleIds.Contains(k.IhaleId))
                .ToListAsync(ct);

            var kalemAggregates = kalemler
                .GroupBy(k => k.IhaleId)
                .ToDictionary(g => g.Key, g => new
                {
                    SutFiyat = g.Sum(x => x.ToplamSutFiyat),
                    SutPuani = g.Sum(x => x.ToplamSUT)
                });

            // 5) Bellekte birleştir — Ozet ekranıyla aynı formül
            var result = ihaleler.Select(i =>
            {
                var personel = personelToplamlar.GetValueOrDefault(i.Id, 0m);
                var ekGider = ekGiderToplamlar.GetValueOrDefault(i.Id, 0m);
                decimal sutFiyat = 0m, sutPuani = 0m;
                if (kalemAggregates.TryGetValue(i.Id, out var kAgg))
                {
                    sutFiyat = kAgg.SutFiyat;
                    sutPuani = kAgg.SutPuani;
                }

                var genelGider = (personel + ekGider) * i.GenelGiderOran;
                var genelToplam = personel + ekGider + genelGider + sutFiyat;
                var maliyetSutOrani = sutPuani > 0 ? genelToplam / sutPuani : 0m;

                return new IhaleAggregate
                {
                    Id = i.Id,
                    IhaleAdi = i.IhaleAdi,
                    IhaleKayitNo = i.IhaleKayitNo,
                    IhaleTarihi = i.IhaleTarihi,
                    IhaleSuresiGun = i.IhaleSuresiGun,
                    KalemSayisi = i.KalemSayisi,
                    GenelToplam = genelToplam,
                    ToplamSutPuani = sutPuani,
                    MaliyetSutOrani = maliyetSutOrani
                };
            }).ToList();

            return result;
        }

        // ──────────────────────────────────────────────────────────────
        // KPI — Paralel sorgular kaldırıldı (DbContext thread-safe değil)
        // ──────────────────────────────────────────────────────────────
        private async Task<KpiSeritVm> BuildKpiAsync(List<IhaleAggregate> ihaleAggregates, CancellationToken ct)
        {
            var now = DateTime.Now;
            var buAyBaslangic = new DateTime(now.Year, now.Month, 1);
            var gecenAyBaslangic = buAyBaslangic.AddMonths(-1);

            var buAyMasraf = await _context.ExpenseReports.AsNoTracking()
                .Where(r => r.MonthYear.Year == now.Year && r.MonthYear.Month == now.Month)
                .SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0m;

            var gecenAyMasraf = await _context.ExpenseReports.AsNoTracking()
                .Where(r => r.MonthYear.Year == gecenAyBaslangic.Year && r.MonthYear.Month == gecenAyBaslangic.Month)
                .SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0m;

            var aktifKullanici = await _context.Users.AsNoTracking()
                .CountAsync(u => u.IsApproved, ct);

            // HB tek sorguda — toplam + farklı hastane
            var hbData = await _context.HospitalBirimler.AsNoTracking()
                .Where(x => x.Aktif)
                .Select(x => x.HospitalID)
                .ToListAsync(ct);

            return new KpiSeritVm
            {
                BuAyMasraf = buAyMasraf,
                GecenAyMasraf = gecenAyMasraf,
                AktifKullaniciSayisi = aktifKullanici,
                AktifIhaleSayisi = ihaleAggregates.Count,
                ToplamIhaleMaliyet = ihaleAggregates.Sum(x => x.GenelToplam),
                AktifHastaneBirimSayisi = hbData.Count,
                FarkliHastaneSayisi = hbData.Distinct().Count()
            };
        }

        // ──────────────────────────────────────────────────────────────
        // Aktif İhaleler — Cache'den
        // ──────────────────────────────────────────────────────────────
        private static List<IhaleOzetCardVm> BuildAktifIhalelerFromAggregates(List<IhaleAggregate> aggregates)
        {
            return aggregates.Take(6).Select(a => new IhaleOzetCardVm
            {
                Id = a.Id,
                IhaleAdi = a.IhaleAdi,
                IhaleKayitNo = a.IhaleKayitNo,
                IhaleTarihi = a.IhaleTarihi,
                IhaleSuresiGun = a.IhaleSuresiGun,
                ToplamMaliyet = a.GenelToplam,
                ToplamSutPuani = a.ToplamSutPuani,
                MaliyetSutOrani = a.MaliyetSutOrani,
                KalemSayisi = a.KalemSayisi
            }).ToList();
        }

        // ──────────────────────────────────────────────────────────────
        // Masraf Analizi — Projection ile
        // ──────────────────────────────────────────────────────────────
        private async Task<MasrafAnalizVm> BuildMasrafAnalizAsync(DateTime start, DateTime end, string periyot, CancellationToken ct)
        {
            var rawDetails = await _context.ExpenseDetails.AsNoTracking()
                .Where(d => d.Date >= start && d.Date <= end)
                .Select(d => new
                {
                    d.Id,
                    d.ReportId,
                    d.Amount,
                    d.Category,
                    d.Description,
                    d.Date,
                    UserId = d.Report.UserId,
                    FullName = d.Report.User.FullName
                })
                .ToListAsync(ct);

            var kullaniciHarcamalari = rawDetails
                .GroupBy(d => new { d.UserId, d.FullName })
                .Select(g => new KullaniciHarcamaVm
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName ?? "Bilinmeyen",
                    Toplam = g.Sum(x => x.Amount),
                    IslemSayisi = g.Count()
                })
                .OrderByDescending(x => x.Toplam)
                .Take(10)
                .ToList();

            var enYuksek = rawDetails
                .OrderByDescending(d => d.Amount)
                .Take(5)
                .Select(d => new EnYuksekMasrafVm
                {
                    ExpenseId = d.Id,
                    ReportId = d.ReportId,
                    FullName = d.FullName ?? "Bilinmeyen",
                    Kategori = d.Category,
                    Aciklama = d.Description,
                    Tarih = d.Date,
                    Tutar = d.Amount
                })
                .ToList();

            return new MasrafAnalizVm
            {
                KullaniciHarcamalari = kullaniciHarcamalari,
                EnYuksekMasraflar = enYuksek,
                Periyot = periyot
            };
        }

        // ──────────────────────────────────────────────────────────────
        // Aylık Trend
        // ──────────────────────────────────────────────────────────────
        private async Task<List<AylikTrendVm>> BuildAylikTrendAsync(CancellationToken ct)
        {
            var now = DateTime.Now;
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

            var raw = await _context.ExpenseReports.AsNoTracking()
                .Where(r => r.MonthYear >= start)
                .GroupBy(r => new { r.MonthYear.Year, r.MonthYear.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Toplam = g.Sum(x => x.TotalAmount)
                })
                .ToListAsync(ct);

            var dict = raw.ToDictionary(x => (x.Year, x.Month), x => x.Toplam);

            var liste = new List<AylikTrendVm>(12);
            for (int i = 0; i < 12; i++)
            {
                var d = start.AddMonths(i);
                liste.Add(new AylikTrendVm
                {
                    Yil = d.Year,
                    Ay = d.Month,
                    AyAdi = d.ToString("MMM yyyy", TR),
                    ToplamMasraf = dict.GetValueOrDefault((d.Year, d.Month), 0m)
                });
            }

            return liste;
        }

        // ──────────────────────────────────────────────────────────────
        // Hastane × Birim — Projection
        // ──────────────────────────────────────────────────────────────
        private async Task<HastaneBirimOzetVm> BuildHastaneBirimOzetAsync(CancellationToken ct)
        {
            var aktif = await _context.HospitalBirimler.AsNoTracking()
                .Where(x => x.Aktif)
                .Select(x => new
                {
                    x.HospitalID,
                    HospitalName = x.Hospital.HospitalName,
                    x.BirimId,
                    BirimKodu = x.Birim.BirimKodu,
                    BirimAdi = x.Birim.BirimAdi
                })
                .ToListAsync(ct);

            var enAktifHastaneler = aktif
                .GroupBy(x => new { x.HospitalID, x.HospitalName })
                .Select(g => new HastaneOzetVm
                {
                    HospitalID = g.Key.HospitalID,
                    HospitalName = g.Key.HospitalName,
                    AktifBirimSayisi = g.Select(x => x.BirimId).Distinct().Count(),
                    BirimKodlari = g.Select(x => x.BirimKodu).Distinct().OrderBy(x => x).ToList()
                })
                .OrderByDescending(x => x.AktifBirimSayisi)
                .Take(5)
                .ToList();

            var birimDagilim = aktif
                .GroupBy(x => new { x.BirimId, x.BirimKodu, x.BirimAdi })
                .Select(g => new BirimDagilimVm
                {
                    BirimKodu = g.Key.BirimKodu,
                    BirimAdi = g.Key.BirimAdi,
                    HastaneSayisi = g.Select(x => x.HospitalID).Distinct().Count()
                })
                .OrderByDescending(x => x.HastaneSayisi)
                .ToList();

            return new HastaneBirimOzetVm
            {
                EnAktifHastaneler = enAktifHastaneler,
                BirimDagilim = birimDagilim
            };
        }

        // ──────────────────────────────────────────────────────────────
        // Bekleyen Onaylar
        // ──────────────────────────────────────────────────────────────
        private async Task<BekleyenOnaylarVm> BuildBekleyenOnaylarAsync(CancellationToken ct)
        {
            var bekleyenler = await _context.Users.AsNoTracking()
                .Where(u => !u.IsApproved)
                .OrderByDescending(u => u.Id)
                .Take(5)
                .Select(u => new BekleyenKullaniciVm
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    Title = u.Title
                })
                .ToListAsync(ct);

            var toplam = await _context.Users.AsNoTracking()
                .CountAsync(u => !u.IsApproved, ct);

            return new BekleyenOnaylarVm
            {
                BekleyenKullaniciSayisi = toplam,
                SonBekleyenler = bekleyenler
            };
        }

        // ──────────────────────────────────────────────────────────────
        // Periyot helper
        // ──────────────────────────────────────────────────────────────
        private static (DateTime start, DateTime end) GetPeriyotRange(string periyot)
        {
            var now = DateTime.Now;
            var endOfDay = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);

            return periyot?.ToLowerInvariant() switch
            {
                "ceyrek" => (new DateTime(now.Year, now.Month, 1).AddMonths(-2), endOfDay),
                "yil" => (new DateTime(now.Year, 1, 1), endOfDay),
                _ => (new DateTime(now.Year, now.Month, 1), endOfDay)
            };
        }

        // Internal DTO
        private class IhaleAggregate
        {
            public int Id { get; set; }
            public string IhaleAdi { get; set; } = "";
            public string? IhaleKayitNo { get; set; }
            public DateTime? IhaleTarihi { get; set; }
            public int IhaleSuresiGun { get; set; }
            public int KalemSayisi { get; set; }
            public decimal GenelToplam { get; set; }
            public decimal ToplamSutPuani { get; set; }
            public decimal MaliyetSutOrani { get; set; }
        }
    }
}