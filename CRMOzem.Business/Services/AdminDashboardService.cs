using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels.Admin;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIhaleService _ihaleService;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public AdminDashboardService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IIhaleService ihaleService)
        {
            _context = context;
            _userManager = userManager;
            _ihaleService = ihaleService;
        }

        public async Task<AdminDashboardVm> GetDashboardAsync(string periyot, CancellationToken ct = default)
        {
            var (rangeStart, rangeEnd) = GetPeriyotRange(periyot);

            var vm = new AdminDashboardVm();

            vm.Kpi = await BuildKpiAsync(ct);
            vm.MasrafAnaliz = await BuildMasrafAnalizAsync(rangeStart, rangeEnd, periyot, ct);
            vm.AylikTrend = await BuildAylikTrendAsync(ct);
            vm.AktifIhaleler = await BuildAktifIhalelerAsync(ct);
            vm.HastaneBirimOzet = await BuildHastaneBirimOzetAsync(ct);
            vm.BekleyenOnaylar = await BuildBekleyenOnaylarAsync(ct);

            return vm;
        }

        // ── KPI ───────────────────────────────────────────────────────
        private async Task<KpiSeritVm> BuildKpiAsync(CancellationToken ct)
        {
            var now = DateTime.Now;
            var buAyBaslangic = new DateTime(now.Year, now.Month, 1);
            var gecenAyBaslangic = buAyBaslangic.AddMonths(-1);
            var gecenAyBitis = buAyBaslangic.AddDays(-1);

            var buAyMasraf = await _context.ExpenseReports
                .Where(r => r.MonthYear.Year == now.Year && r.MonthYear.Month == now.Month)
                .SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0m;

            var gecenAyMasraf = await _context.ExpenseReports
                .Where(r => r.MonthYear.Year == gecenAyBaslangic.Year && r.MonthYear.Month == gecenAyBaslangic.Month)
                .SumAsync(r => (decimal?)r.TotalAmount, ct) ?? 0m;

            // Aktif kullanıcı: IsApproved = true
            var aktifKullanici = await _context.Users.CountAsync(u => u.IsApproved, ct);

            // Aktif ihaleler
            var aktifIhaleIds = await _context.Ihaleler.AsNoTracking()
                .Where(i => i.Aktif)
                .Select(i => i.Id)
                .ToListAsync(ct);

            decimal toplamMaliyet = 0;
            foreach (var ihaleId in aktifIhaleIds)
            {
                var ozet = await _ihaleService.GetOzetAsync(ihaleId);
                toplamMaliyet += ozet.GenelToplam;
            }

            // Hastane × Birim
            var hbList = await _context.HospitalBirimler.AsNoTracking()
                .Where(x => x.Aktif)
                .Select(x => x.HospitalID)
                .ToListAsync(ct);

            return new KpiSeritVm
            {
                BuAyMasraf = buAyMasraf,
                GecenAyMasraf = gecenAyMasraf,
                AktifKullaniciSayisi = aktifKullanici,
                AktifIhaleSayisi = aktifIhaleIds.Count,
                ToplamIhaleMaliyet = toplamMaliyet,
                AktifHastaneBirimSayisi = hbList.Count,
                FarkliHastaneSayisi = hbList.Distinct().Count()
            };
        }

        // ── Masraf Analizi ────────────────────────────────────────────
        private async Task<MasrafAnalizVm> BuildMasrafAnalizAsync(DateTime start, DateTime end, string periyot, CancellationToken ct)
        {
            var details = await _context.ExpenseDetails.AsNoTracking()
                .Include(d => d.Report).ThenInclude(r => r.User)
                .Where(d => d.Date >= start && d.Date <= end)
                .ToListAsync(ct);

            var kullaniciHarcamalari = details
                .GroupBy(d => new { d.Report.UserId, d.Report.User.FullName })
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

            var enYuksek = details
                .OrderByDescending(d => d.Amount)
                .Take(5)
                .Select(d => new EnYuksekMasrafVm
                {
                    ExpenseId = d.Id,
                    ReportId = d.ReportId,
                    FullName = d.Report.User?.FullName ?? "Bilinmeyen",
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

        // ── Aylık Trend ───────────────────────────────────────────────
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

            // 12 ayın tamamını üret (boş aylar için 0)
            var liste = new List<AylikTrendVm>();
            for (int i = 0; i < 12; i++)
            {
                var d = start.AddMonths(i);
                var match = raw.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
                liste.Add(new AylikTrendVm
                {
                    Yil = d.Year,
                    Ay = d.Month,
                    AyAdi = d.ToString("MMM yyyy", TR),
                    ToplamMasraf = match?.Toplam ?? 0
                });
            }

            return liste;
        }

        // ── Aktif İhaleler ────────────────────────────────────────────
        private async Task<List<IhaleOzetCardVm>> BuildAktifIhalelerAsync(CancellationToken ct)
        {
            // Önce aktif ihaleleri listele (sadece header bilgisi + kalem sayısı için).
            var ihaleler = await _context.Ihaleler.AsNoTracking()
                .Where(i => i.Aktif)
                .Include(i => i.IhaleKalemler)
                .OrderByDescending(i => i.OlusturmaTarihi)
                .Take(6)
                .ToListAsync(ct);

            var result = new List<IhaleOzetCardVm>();

            // Her ihale için IhaleService'in resmi Ozet hesabını kullan — Ozet ekranıyla bire bir uyumlu.
            foreach (var ih in ihaleler)
            {
                var ozet = await _ihaleService.GetOzetAsync(ih.Id);

                result.Add(new IhaleOzetCardVm
                {
                    Id = ih.Id,
                    IhaleAdi = ih.IhaleAdi,
                    IhaleKayitNo = ih.IhaleKayitNo,
                    IhaleTarihi = ih.IhaleTarihi,
                    IhaleSuresiGun = ih.IhaleSuresiGun,
                    ToplamMaliyet = ozet.GenelToplam,            // ✅ Artık Ozet ekranıyla aynı
                    ToplamSutPuani = ozet.ToplamSutPuani,
                    MaliyetSutOrani = ozet.ToplamMaliyetSutPuani,
                    KalemSayisi = ih.IhaleKalemler.Count
                });
            }

            return result;
        }

        // ── Hastane × Birim Özet ──────────────────────────────────────
        private async Task<HastaneBirimOzetVm> BuildHastaneBirimOzetAsync(CancellationToken ct)
        {
            var aktif = await _context.HospitalBirimler.AsNoTracking()
                .Where(x => x.Aktif)
                .Include(x => x.Hospital)
                .Include(x => x.Birim)
                .ToListAsync(ct);

            var enAktifHastaneler = aktif
                .GroupBy(x => new { x.HospitalID, x.Hospital.HospitalName })
                .Select(g => new HastaneOzetVm
                {
                    HospitalID = g.Key.HospitalID,
                    HospitalName = g.Key.HospitalName,
                    AktifBirimSayisi = g.Select(x => x.BirimId).Distinct().Count(),
                    BirimKodlari = g.Select(x => x.Birim.BirimKodu).Distinct().OrderBy(x => x).ToList()
                })
                .OrderByDescending(x => x.AktifBirimSayisi)
                .Take(5)
                .ToList();

            var birimDagilim = aktif
                .GroupBy(x => new { x.BirimId, x.Birim.BirimKodu, x.Birim.BirimAdi })
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

        // ── Bekleyen Onaylar ──────────────────────────────────────────
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

            var toplam = await _context.Users.CountAsync(u => !u.IsApproved, ct);

            return new BekleyenOnaylarVm
            {
                BekleyenKullaniciSayisi = toplam,
                SonBekleyenler = bekleyenler
            };
        }

        // ── Periyot helper ────────────────────────────────────────────
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
    }
}