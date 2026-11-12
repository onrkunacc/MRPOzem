using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly ApplicationDbContext _context;

        // ═══════════════════════════════════════════════════════
        //  DURUM KODLARI — LeaveSeeder ile birebir aynı olmalı
        // ═══════════════════════════════════════════════════════
        //  Pending            → 1. aşama: çalışanın yöneticisi karar verecek
        //  SupervisorApproved → 2. aşama: yönetici onayladı, İK karar verecek
        //  Approved / Rejected / Cancelled → kapandı

        private const string KOD_BEKLIYOR = "Pending";
        private const string KOD_YONETICI_ONAYLADI = "SupervisorApproved";
        private const string KOD_ONAYLANDI = "Approved";
        private const string KOD_REDDEDILDI = "Rejected";
        private const string KOD_IPTAL = "Cancelled";

        /// <summary>
        /// Henüz karara bağlanmamış durumlar. Bakiyedeki "bekleyen gün",
        /// çakışma kontrolü ve iptal izni bu kümeye bakar — iki aşamadan
        /// hangisinde olduğu fark etmez, gün hâlâ rezerve demektir.
        /// </summary>
        private static readonly string[] ACIK_KODLAR = { KOD_BEKLIYOR, KOD_YONETICI_ONAYLADI };

        /// <summary>
        /// İzin takibine dahil edilecek roller.
        /// Bu rollerden EN AZ BİRİNE sahip kullanıcılar bakiye ve personel
        /// ekranlarında listelenir; diğerleri (sistem/servis hesapları) gizlenir.
        ///
        /// Bir çalışan listede görünmüyorsa rolünü buraya ekleyin:
        ///     { "Kullanıcı", "Lojistik", "Muhasebe" }
        /// Tüm kullanıcıları göstermek için diziyi boşaltın: { }
        /// </summary>
        private static readonly string[] CALISAN_ROLLERI = { "Kullanıcı" };

        public LeaveService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════════════
        //  ÇALIŞAN SÜZGECİ
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// İzin takibine dahil kullanıcı id'leri.
        /// CALISAN_ROLLERI boşsa null döner — o durumda süzme yapılmaz.
        /// </summary>
        private async Task<HashSet<string>?> GetCalisanIdleriAsync()
        {
            if (CALISAN_ROLLERI.Length == 0) return null;

            var idler = await (from ur in _context.UserRoles
                               join r in _context.Roles on ur.RoleId equals r.Id
                               where CALISAN_ROLLERI.Contains(r.Name)
                               select ur.UserId)
                              .Distinct()
                              .ToListAsync();

            return idler.ToHashSet();
        }

        // ═══════════════════════════════════════════════════════
        //  GÜN HESABI
        // ═══════════════════════════════════════════════════════

        public async Task<int> HesaplaIsGunuAsync(DateTime baslangic, DateTime bitis, bool cumartesiHaric)
        {
            var bas = baslangic.Date;
            var bit = bitis.Date;
            if (bit < bas) return 0;

            var tatiller = await _context.Set<ResmiTatil>()
                .AsNoTracking()
                .Where(t => t.TamGun && t.Tarih >= bas && t.Tarih <= bit)
                .Select(t => t.Tarih)
                .ToListAsync();

            var tatilSet = tatiller.Select(t => t.Date).ToHashSet();

            int gun = 0;
            for (var d = bas; d <= bit; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;
                if (cumartesiHaric && d.DayOfWeek == DayOfWeek.Saturday) continue;
                if (tatilSet.Contains(d)) continue;
                gun++;
            }

            return gun;
        }

        // ═══════════════════════════════════════════════════════
        //  BAKİYE
        // ═══════════════════════════════════════════════════════

        public async Task<LeaveBalanceVm> GetBalanceAsync(string userId, int yil)
        {
            var kayit = await _context.UserLeaveBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == yil);

            var u = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new
                {
                    x.FullName,
                    x.UserName,
                    x.Title,
                    x.Departman,
                    x.IseGirisTarihi,
                    x.DogumTarihi
                })
                .FirstOrDefaultAsync();

            var talepler = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => l.UserId == userId && l.StartDate.Year == yil && l.LeaveType.IsDeductedFromQuota)
                .Select(l => new { l.TotalDays, Kod = l.Status.Code })
                .ToListAsync();

            var kullanilan = talepler.Where(x => x.Kod == KOD_ONAYLANDI).Sum(x => x.TotalDays);

            // Yönetici onayında VE İK onayında olanlar birlikte "bekleyen" sayılır
            var bekleyen = talepler.Where(x => x.Kod != null && ACIK_KODLAR.Contains(x.Kod))
                                   .Sum(x => x.TotalDays);

            return new LeaveBalanceVm
            {
                UserId = userId,
                FullName = u?.FullName ?? u?.UserName ?? "—",
                Title = u?.Title,
                Departman = u?.Departman,
                Year = yil,
                IseGirisTarihi = u?.IseGirisTarihi,
                DogumTarihi = u?.DogumTarihi,
                TotalDays = kayit?.TotalDays ?? 0,
                CarriedOverDays = kayit?.CarriedOverDays ?? 0,
                UsedDays = kullanilan,
                PendingDays = bekleyen,
                ManuelGirildi = kayit?.ManuelGirildi ?? false,
                HasRecord = kayit != null,
                KidemYili = IzinHakedisHesaplayici.KidemYili(u?.IseGirisTarihi, yil),
                OnerilenHakedis = IzinHakedisHesaplayici.Hesapla(u?.IseGirisTarihi, u?.DogumTarihi, yil),
                HakedisAciklamasi = IzinHakedisHesaplayici.Aciklama(u?.IseGirisTarihi, u?.DogumTarihi, yil)
            };
        }

        public async Task<int> GetRemainingLeaveDaysAsync(string userId)
        {
            var b = await GetBalanceAsync(userId, DateTime.Now.Year);
            return b.RemainingDays;
        }

        public async Task<List<LeaveBalanceVm>> GetAllBalancesAsync(int yil)
        {
            var calisanIdler = await GetCalisanIdleriAsync();

            var q = _context.Users.AsNoTracking().AsQueryable();
            if (calisanIdler != null)
                q = q.Where(u => calisanIdler.Contains(u.Id));

            var kullanicilar = await q
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName,
                    u.Title,
                    u.Departman,
                    u.IseGirisTarihi,
                    u.DogumTarihi,
                    u.IstenCikisTarihi
                })
                .ToListAsync();

            var kayitlar = await _context.UserLeaveBalances
                .AsNoTracking()
                .Where(b => b.Year == yil)
                .ToListAsync();

            var talepler = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => l.StartDate.Year == yil && l.LeaveType.IsDeductedFromQuota)
                .Select(l => new { l.UserId, l.TotalDays, Kod = l.Status.Code })
                .ToListAsync();

            var kullanimMap = talepler.Where(x => x.Kod == KOD_ONAYLANDI)
                .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalDays));

            var bekleyenMap = talepler.Where(x => x.Kod != null && ACIK_KODLAR.Contains(x.Kod))
                .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalDays));

            var kayitMap = kayitlar.ToDictionary(k => k.UserId);

            return kullanicilar
                .Select(u =>
                {
                    kayitMap.TryGetValue(u.Id, out var kayit);
                    return new LeaveBalanceVm
                    {
                        UserId = u.Id,
                        FullName = u.FullName ?? u.UserName ?? "—",
                        Title = u.Title,
                        Departman = u.Departman,
                        Year = yil,
                        IseGirisTarihi = u.IseGirisTarihi,
                        DogumTarihi = u.DogumTarihi,
                        TotalDays = kayit?.TotalDays ?? 0,
                        CarriedOverDays = kayit?.CarriedOverDays ?? 0,
                        UsedDays = kullanimMap.TryGetValue(u.Id, out var k) ? k : 0,
                        PendingDays = bekleyenMap.TryGetValue(u.Id, out var b) ? b : 0,
                        ManuelGirildi = kayit?.ManuelGirildi ?? false,
                        HasRecord = kayit != null,
                        KidemYili = IzinHakedisHesaplayici.KidemYili(u.IseGirisTarihi, yil),
                        OnerilenHakedis = IzinHakedisHesaplayici.Hesapla(u.IseGirisTarihi, u.DogumTarihi, yil),
                        HakedisAciklamasi = IzinHakedisHesaplayici.Aciklama(u.IseGirisTarihi, u.DogumTarihi, yil)
                    };
                })
                .OrderBy(x => x.FullName)
                .ToList();
        }

        public async Task<(bool Basarili, string? Hata)> SetEntitlementAsync(
            string userId, int yil, int hakedis, int devreden, string? aciklama = null)
        {
            if (hakedis < 0 || devreden < 0)
                return (false, "Gün sayısı negatif olamaz.");

            if (hakedis > 365 || devreden > 365)
                return (false, "Gün sayısı makul bir aralıkta olmalı.");

            var kullanici = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IseGirisTarihi })
                .FirstOrDefaultAsync();

            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı.");

            var kayit = await _context.UserLeaveBalances
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == yil);

            if (kayit == null)
            {
                kayit = new UserLeaveBalance { UserId = userId, Year = yil, UsedDays = 0 };
                _context.UserLeaveBalances.Add(kayit);
            }

            kayit.TotalDays = hakedis;
            kayit.CarriedOverDays = devreden;
            kayit.ManuelGirildi = true;   // elle girildi — otomatik hesaplama ezmeyecek
            kayit.KidemYili = IzinHakedisHesaplayici.KidemYili(kullanici.IseGirisTarihi, yil);
            kayit.Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim();

            await _context.SaveChangesAsync();
            await RecalculateBalanceAsync(userId, yil);
            return (true, null);
        }

        public async Task<(bool Basarili, string? Hata, int Hakedis)> OtomatikHakedisAsync(
            string userId, int yil, bool devriAktar)
        {
            var u = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new { x.IseGirisTarihi, x.DogumTarihi })
                .FirstOrDefaultAsync();

            if (u == null)
                return (false, "Kullanıcı bulunamadı.", 0);

            if (u.IseGirisTarihi == null)
                return (false, "İşe giriş tarihi girilmemiş. Önce personel kartından giriniz.", 0);

            var hakedis = IzinHakedisHesaplayici.Hesapla(u.IseGirisTarihi, u.DogumTarihi, yil);
            var devreden = devriAktar ? await HesaplaDevredenAsync(userId, yil) : 0;

            var kayit = await _context.UserLeaveBalances
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == yil);

            if (kayit == null)
            {
                kayit = new UserLeaveBalance { UserId = userId, Year = yil, UsedDays = 0 };
                _context.UserLeaveBalances.Add(kayit);
            }

            kayit.TotalDays = hakedis;
            kayit.CarriedOverDays = devreden;
            kayit.ManuelGirildi = false;
            kayit.KidemYili = IzinHakedisHesaplayici.KidemYili(u.IseGirisTarihi, yil);
            kayit.Aciklama = IzinHakedisHesaplayici.Aciklama(u.IseGirisTarihi, u.DogumTarihi, yil);

            await _context.SaveChangesAsync();
            await RecalculateBalanceAsync(userId, yil);

            return (true, null, hakedis);
        }

        public async Task<(int Olusturulan, int Guncellenen, int Atlanan)> YilToplamOlusturAsync(
            int yil, bool devriAktar, bool manuelleriEz)
        {
            var calisanIdler = await GetCalisanIdleriAsync();

            var q = _context.Users
                .AsNoTracking()
                .Where(u => u.IseGirisTarihi != null
                         && (u.IstenCikisTarihi == null || u.IstenCikisTarihi > DateTime.Today));

            if (calisanIdler != null)
                q = q.Where(u => calisanIdler.Contains(u.Id));

            var kullanicilar = await q
                .Select(u => new { u.Id, u.IseGirisTarihi, u.DogumTarihi })
                .ToListAsync();

            var mevcut = await _context.UserLeaveBalances
                .Where(b => b.Year == yil)
                .ToListAsync();

            var mevcutMap = mevcut.ToDictionary(b => b.UserId);

            int olusturulan = 0, guncellenen = 0, atlanan = 0;

            foreach (var u in kullanicilar)
            {
                var hakedis = IzinHakedisHesaplayici.Hesapla(u.IseGirisTarihi, u.DogumTarihi, yil);
                var kidem = IzinHakedisHesaplayici.KidemYili(u.IseGirisTarihi, yil);
                var aciklama = IzinHakedisHesaplayici.Aciklama(u.IseGirisTarihi, u.DogumTarihi, yil);

                if (mevcutMap.TryGetValue(u.Id, out var kayit))
                {
                    // Elle girilmiş kayda dokunma
                    if (kayit.ManuelGirildi && !manuelleriEz)
                    {
                        atlanan++;
                        continue;
                    }

                    kayit.TotalDays = hakedis;
                    kayit.ManuelGirildi = false;
                    kayit.KidemYili = kidem;
                    kayit.Aciklama = aciklama;

                    if (devriAktar)
                        kayit.CarriedOverDays = await HesaplaDevredenAsync(u.Id, yil);

                    guncellenen++;
                }
                else
                {
                    _context.UserLeaveBalances.Add(new UserLeaveBalance
                    {
                        UserId = u.Id,
                        Year = yil,
                        TotalDays = hakedis,
                        UsedDays = 0,
                        CarriedOverDays = devriAktar ? await HesaplaDevredenAsync(u.Id, yil) : 0,
                        ManuelGirildi = false,
                        KidemYili = kidem,
                        Aciklama = aciklama
                    });
                    olusturulan++;
                }
            }

            await _context.SaveChangesAsync();

            foreach (var u in kullanicilar)
                await RecalculateBalanceAsync(u.Id, yil);

            return (olusturulan, guncellenen, atlanan);
        }

        /// <summary>Bir önceki yıldan artan gün — devreden olarak aktarılır.</summary>
        private async Task<int> HesaplaDevredenAsync(string userId, int yil)
        {
            var oncekiYil = yil - 1;

            var onceki = await _context.UserLeaveBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == oncekiYil);

            if (onceki == null) return 0;

            var kullanilan = await _context.LeaveRequests
                .Where(l => l.UserId == userId
                         && l.StartDate.Year == oncekiYil
                         && l.LeaveType.IsDeductedFromQuota
                         && l.Status.Code == KOD_ONAYLANDI)
                .SumAsync(l => (int?)l.TotalDays) ?? 0;

            var kalan = (onceki.TotalDays + onceki.CarriedOverDays) - kullanilan;
            return Math.Max(0, kalan);
        }

        public async Task RecalculateBalanceAsync(string userId, int yil)
        {
            var kayit = await _context.UserLeaveBalances
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == yil);

            if (kayit == null) return;

            var kullanilan = await _context.LeaveRequests
                .Where(l => l.UserId == userId
                         && l.StartDate.Year == yil
                         && l.LeaveType.IsDeductedFromQuota
                         && l.Status.Code == KOD_ONAYLANDI)
                .SumAsync(l => (int?)l.TotalDays) ?? 0;

            if (kayit.UsedDays != kullanilan)
            {
                kayit.UsedDays = kullanilan;
                await _context.SaveChangesAsync();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  PERSONEL ÖZLÜK
        // ═══════════════════════════════════════════════════════

        public async Task<PersonelListVm> GetPersonelListAsync(bool sadeceAktif = false)
        {
            var calisanIdler = await GetCalisanIdleriAsync();

            var q = _context.Users.AsNoTracking().AsQueryable();

            if (calisanIdler != null)
                q = q.Where(u => calisanIdler.Contains(u.Id));

            if (sadeceAktif)
                q = q.Where(u => u.IstenCikisTarihi == null || u.IstenCikisTarihi > DateTime.Today);

            var kullanicilar = await q
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName,
                    u.Title,
                    u.Departman,
                    u.Email,
                    u.TcKimlikNo,
                    u.IseGirisTarihi,
                    u.DogumTarihi,
                    u.IstenCikisTarihi
                })
                .ToListAsync();

            var buYil = DateTime.Now.Year;

            // ── Organizasyon bağları ──
            var baglar = await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Select(x => new
                {
                    x.UserId,
                    x.SupervisorId,
                    YoneticiAd = x.Supervisor.FullName ?? x.Supervisor.UserName
                })
                .ToListAsync();

            var yoneticiMap = baglar
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var astSayiMap = baglar
                .GroupBy(x => x.SupervisorId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).Distinct().Count());

            var liste = kullanicilar
                .Select(u =>
                {
                    yoneticiMap.TryGetValue(u.Id, out var yon);

                    return new PersonelSatirVm
                    {
                        UserId = u.Id,
                        FullName = u.FullName ?? u.UserName ?? "—",
                        UserName = u.UserName,
                        Title = u.Title,
                        Departman = u.Departman,
                        Email = u.Email,
                        TcKimlikNo = u.TcKimlikNo,
                        IseGirisTarihi = u.IseGirisTarihi,
                        DogumTarihi = u.DogumTarihi,
                        IstenCikisTarihi = u.IstenCikisTarihi,
                        Aktif = u.IstenCikisTarihi == null || u.IstenCikisTarihi.Value.Date > DateTime.Today,
                        Kidem = IzinHakedisHesaplayici.GuncelKidem(u.IseGirisTarihi),
                        Yas = IzinHakedisHesaplayici.GuncelYas(u.DogumTarihi),
                        BuYilHakedis = IzinHakedisHesaplayici.Hesapla(u.IseGirisTarihi, u.DogumTarihi, buYil),
                        YoneticiUserId = yon?.SupervisorId,
                        YoneticiAdi = yon?.YoneticiAd,
                        AstSayisi = astSayiMap.TryGetValue(u.Id, out var ast) ? ast : 0
                    };
                })
                .OrderByDescending(x => x.Aktif)
                .ThenBy(x => x.FullName)
                .ToList();

            return new PersonelListVm { Personeller = liste };
        }

        public async Task<PersonelEditVm?> GetPersonelAsync(string userId)
        {
            var u = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (u == null) return null;

            var buYil = DateTime.Now.Year;

            var vm = new PersonelEditVm
            {
                UserId = u.Id,
                FullName = u.FullName ?? u.UserName ?? "—",
                UserName = u.UserName,
                Title = u.Title,
                Departman = u.Departman,
                TcKimlikNo = u.TcKimlikNo,
                IseGirisTarihi = u.IseGirisTarihi,
                DogumTarihi = u.DogumTarihi,
                IstenCikisTarihi = u.IstenCikisTarihi,
                Kidem = IzinHakedisHesaplayici.GuncelKidem(u.IseGirisTarihi),
                Yas = IzinHakedisHesaplayici.GuncelYas(u.DogumTarihi),
                BuYilHakedis = IzinHakedisHesaplayici.Hesapla(u.IseGirisTarihi, u.DogumTarihi, buYil),
                HakedisAciklamasi = IzinHakedisHesaplayici.Aciklama(u.IseGirisTarihi, u.DogumTarihi, buYil),
                YoneticiAdi = await GetYoneticiAdiAsync(userId),
                AstSayisi = await _context.Set<UserSupervisor>()
                    .AsNoTracking()
                    .CountAsync(x => x.SupervisorId == userId)
            };

            // Son 5 yılın bakiyeleri
            for (int y = buYil; y > buYil - 5; y--)
            {
                var b = await GetBalanceAsync(userId, y);
                if (b.HasRecord || b.UsedDays > 0)
                    vm.Bakiyeler.Add(b);
            }

            return vm;
        }

        public async Task<(bool Basarili, string? Hata)> UpdatePersonelAsync(PersonelEditVm model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserId))
                return (false, "Geçersiz istek.");

            var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == model.UserId);
            if (u == null)
                return (false, "Kullanıcı bulunamadı.");

            if (model.IseGirisTarihi.HasValue && model.IseGirisTarihi.Value.Date > DateTime.Today.AddYears(1))
                return (false, "İşe giriş tarihi çok ileri bir tarih.");

            if (model.DogumTarihi.HasValue)
            {
                var yas = IzinHakedisHesaplayici.GuncelYas(model.DogumTarihi);
                if (yas < 15 || yas > 90)
                    return (false, "Doğum tarihi makul bir aralıkta olmalı.");
            }

            if (model.IseGirisTarihi.HasValue && model.IstenCikisTarihi.HasValue
                && model.IstenCikisTarihi.Value.Date < model.IseGirisTarihi.Value.Date)
                return (false, "İşten çıkış tarihi işe giriş tarihinden önce olamaz.");

            if (!string.IsNullOrWhiteSpace(model.TcKimlikNo) && model.TcKimlikNo.Trim().Length != 11)
                return (false, "TC kimlik no 11 haneli olmalıdır.");

            u.Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title.Trim();
            u.Departman = string.IsNullOrWhiteSpace(model.Departman) ? null : model.Departman.Trim();
            u.TcKimlikNo = string.IsNullOrWhiteSpace(model.TcKimlikNo) ? null : model.TcKimlikNo.Trim();
            u.IseGirisTarihi = model.IseGirisTarihi?.Date;
            u.DogumTarihi = model.DogumTarihi?.Date;
            u.IstenCikisTarihi = model.IstenCikisTarihi?.Date;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        // ═══════════════════════════════════════════════════════
        //  TALEP OLUŞTURMA
        // ═══════════════════════════════════════════════════════

        public async Task<(bool Basarili, string? Hata)> CreateLeaveAsync(LeaveRequest request)
        {
            if (request == null)
                return (false, "Talep boş olamaz.");

            request.StartDate = request.StartDate.Date;
            request.EndDate = request.EndDate.Date;

            if (request.EndDate < request.StartDate)
                return (false, "Bitiş tarihi başlangıç tarihinden önce olamaz.");

            if (request.StartDate.Year != request.EndDate.Year)
                return (false, "İzin tek bir yıl içinde olmalıdır. Yıl geçişi için iki ayrı talep giriniz.");

            var tur = await _context.LeaveTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId);
            if (tur == null)
                return (false, "Geçersiz izin türü.");

            // ── Çakışma kontrolü ──
            // Onaylı olanlar kadar henüz karar bekleyenler de çakışma sayılır.
            var cakisan = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => l.UserId == request.UserId
                         && (l.Status.Code == KOD_ONAYLANDI || ACIK_KODLAR.Contains(l.Status.Code))
                         && l.StartDate <= request.EndDate
                         && l.EndDate >= request.StartDate)
                .Select(l => new { l.StartDate, l.EndDate })
                .FirstOrDefaultAsync();

            if (cakisan != null)
                return (false, $"Bu tarihler {cakisan.StartDate:dd.MM.yyyy} – {cakisan.EndDate:dd.MM.yyyy} " +
                               "tarihli mevcut talebinizle çakışıyor.");

            // ── Gün hesabı ──
            var gun = await HesaplaIsGunuAsync(request.StartDate, request.EndDate, request.SaturdayExcluded);
            if (gun <= 0)
                return (false, "Seçtiğiniz aralıkta iş günü bulunmuyor. " +
                               "Tüm günler hafta tatili veya resmi tatile denk geliyor.");

            request.TotalDays = gun;

            // ── Bakiye kontrolü ──
            if (tur.IsDeductedFromQuota)
            {
                var bakiye = await GetBalanceAsync(request.UserId, request.StartDate.Year);

                if (!bakiye.HasRecord)
                    return (false, $"{request.StartDate.Year} yılı için izin hakedişiniz tanımlanmamış. " +
                                   "Lütfen İnsan Kaynakları ile görüşün.");

                if (gun > bakiye.ProjectedRemainingDays)
                    return (false, $"Yetersiz izin bakiyesi. Talep {gun} gün, " +
                                   $"kullanılabilir {bakiye.ProjectedRemainingDays} gün " +
                                   $"(hakediş {bakiye.Entitlement}, kullanılan {bakiye.UsedDays}, " +
                                   $"bekleyen {bakiye.PendingDays}).");
            }

            // ── Yönetici ataması: talep hangi aşamada açılacak? ──
            // Yöneticisi varsa 1. aşamada (Pending) açılır ve yönetici bilgisi
            // kayda dondurulur. Yöneticisi yoksa 1. aşama atlanır, talep
            // doğrudan İK onayına (SupervisorApproved) düşer — yoksa kimse
            // onaylayamadığı için talep sonsuza kadar askıda kalırdı.
            var yonetici = await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Where(x => x.UserId == request.UserId)
                .Select(x => new { x.SupervisorId, x.Supervisor.FullName, x.Supervisor.UserName })
                .FirstOrDefaultAsync();

            string hedefKod;

            if (yonetici != null && !string.IsNullOrEmpty(yonetici.SupervisorId))
            {
                request.SupervisorUserId = yonetici.SupervisorId;
                request.SupervisorName = yonetici.FullName ?? yonetici.UserName;
                request.YoneticisizGecildi = false;
                hedefKod = KOD_BEKLIYOR;
            }
            else
            {
                request.SupervisorUserId = null;
                request.SupervisorName = null;
                request.YoneticisizGecildi = true;
                hedefKod = KOD_YONETICI_ONAYLADI;
            }

            var durum = await _context.LeaveStatuses.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == hedefKod);
            if (durum == null)
                return (false, $"Sistem hatası: '{hedefKod}' izin durumu tanımlı değil. " +
                               "Uygulama yeniden başlatılarak durum tanımlarının oluşması gerekiyor.");

            request.StatusId = durum.Id;
            request.CreatedAt = DateTime.Now;
            request.SupervisorOnayladi = null;
            request.SupervisorDecisionAt = null;
            request.SupervisorNote = null;
            request.DecisionByUserId = null;
            request.DecisionByName = null;
            request.DecisionAt = null;
            request.DecisionNote = null;

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            return (true, null);
        }


        public async Task<(bool Basarili, string? Hata)> ApproveLeaveAsync(
            int leaveRequestId, string approverUserId, string? not = null)
        {
            var talep = await _context.LeaveRequests
                .Include(l => l.Status)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.Id == leaveRequestId);

            if (talep == null)
                return (false, "İzin talebi bulunamadı.");

            var mevcutKod = talep.Status?.Code;

            // İK son söz sahibi: yönetici onayından geçmiş talebi de,
            // henüz yönetici onayında bekleyen talebi de onaylayabilir.
            if (mevcutKod == null || !ACIK_KODLAR.Contains(mevcutKod))
                return (false, $"Bu talep zaten işlem görmüş ({talep.Status?.Name}). Tekrar onaylanamaz.");

            if (talep.LeaveType?.IsDeductedFromQuota == true)
            {
                var bakiye = await GetBalanceAsync(talep.UserId, talep.StartDate.Year);
                if (talep.TotalDays > bakiye.RemainingDays)
                    return (false, $"Yetersiz bakiye: talep {talep.TotalDays} gün, " +
                                   $"çalışanın kalan hakkı {bakiye.RemainingDays} gün.");
            }

            var durum = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == KOD_ONAYLANDI);
            if (durum == null)
                return (false, "Sistem hatası: 'Approved' izin durumu tanımlı değil.");

            var yoneticiAtlandi = mevcutKod == KOD_BEKLIYOR && !talep.YoneticisizGecildi;

            talep.StatusId = durum.Id;
            talep.DecisionByUserId = approverUserId;
            talep.DecisionByName = await GetUserDisplayNameAsync(approverUserId);
            talep.DecisionAt = DateTime.Now;

            var temizNot = string.IsNullOrWhiteSpace(not) ? null : not.Trim();

            // Yönetici onayı beklenmeden İK onayladıysa bu kayda geçsin —
            // sonradan "bu nasıl onaylandı" sorusunun cevabı burada olur.
            if (yoneticiAtlandi)
            {
                var ek = $"Yönetici onayı ({talep.SupervisorName ?? "atanmış yönetici"}) " +
                         "beklenmeden İK tarafından onaylandı.";
                temizNot = string.IsNullOrEmpty(temizNot) ? ek : $"{temizNot} — {ek}";
            }

            talep.DecisionNote = Kirp(temizNot, 1000);

            await _context.SaveChangesAsync();
            await RecalculateBalanceAsync(talep.UserId, talep.StartDate.Year);

            return (true, null);
        }

        public async Task<(bool Basarili, string? Hata)> RejectLeaveAsync(
            int leaveRequestId, string approverUserId, string gerekce)
        {
            if (string.IsNullOrWhiteSpace(gerekce))
                return (false, "Red gerekçesi zorunludur.");

            var talep = await _context.LeaveRequests
                .Include(l => l.Status)
                .FirstOrDefaultAsync(l => l.Id == leaveRequestId);

            if (talep == null)
                return (false, "İzin talebi bulunamadı.");

            var mevcutKod = talep.Status?.Code;

            if (mevcutKod == null || !ACIK_KODLAR.Contains(mevcutKod))
                return (false, $"Bu talep zaten işlem görmüş ({talep.Status?.Name}). Tekrar reddedilemez.");

            var durum = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == KOD_REDDEDILDI);
            if (durum == null)
                return (false, "Sistem hatası: 'Rejected' izin durumu tanımlı değil.");

            talep.StatusId = durum.Id;
            talep.DecisionByUserId = approverUserId;
            talep.DecisionByName = await GetUserDisplayNameAsync(approverUserId);
            talep.DecisionAt = DateTime.Now;
            talep.DecisionNote = Kirp(gerekce.Trim(), 1000);

            await _context.SaveChangesAsync();
            await RecalculateBalanceAsync(talep.UserId, talep.StartDate.Year);

            return (true, null);
        }

        public async Task<(bool Basarili, string? Hata)> CancelLeaveAsync(int leaveRequestId, string userId)
        {
            var talep = await _context.LeaveRequests
                .Include(l => l.Status)
                .FirstOrDefaultAsync(l => l.Id == leaveRequestId);

            if (talep == null)
                return (false, "İzin talebi bulunamadı.");

            if (talep.UserId != userId)
                return (false, "Bu talep size ait değil.");

            var kod = talep.Status?.Code;

            if (kod == KOD_IPTAL)
                return (false, "Bu talep zaten iptal edilmiş.");

            if (kod == KOD_REDDEDILDI)
                return (false, "Reddedilmiş talep iptal edilemez.");

            if (kod == KOD_ONAYLANDI && talep.StartDate.Date <= DateTime.Today)
                return (false, "Başlamış veya tamamlanmış izin iptal edilemez. İnsan Kaynakları ile görüşün.");

            // Yönetici onayında ya da İK onayında olan talep her zaman iptal edilebilir —
            // çalışan kendi talebini geri çekiyor.

            var durum = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == KOD_IPTAL);
            if (durum == null)
                return (false, "Sistem hatası: 'Cancelled' izin durumu tanımlı değil.");

            talep.StatusId = durum.Id;
            talep.DecisionAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await RecalculateBalanceAsync(talep.UserId, talep.StartDate.Year);

            return (true, null);
        }



        public async Task<List<LeaveRequest>> GetUserLeavesAsync(string userId, int? yil = null)
        {
            var q = _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.Status)
                .Where(l => l.UserId == userId);

            if (yil.HasValue)
                q = q.Where(l => l.StartDate.Year == yil.Value);

            return await q
                .OrderByDescending(l => l.StartDate)
                .ThenByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<LeaveRequest>> GetAllAsync(int? yil = null, string? statusCode = null)
        {
            var q = _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.User)
                .Include(l => l.Status)
                .AsQueryable();

            if (yil.HasValue)
                q = q.Where(l => l.StartDate.Year == yil.Value);

            if (!string.IsNullOrWhiteSpace(statusCode))
                q = q.Where(l => l.Status.Code == statusCode);

            return await q
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<LeaveRequest?> GetByIdAsync(int id)
        {
            return await _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.User)
                .Include(l => l.Status)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<bool> IsOwnerAsync(int leaveRequestId, string userId)
        {
            return await _context.LeaveRequests
                .AnyAsync(l => l.Id == leaveRequestId && l.UserId == userId);
        }

        public async Task<List<LeaveType>> GetLeaveTypesAsync()
        {
            return await _context.LeaveTypes
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<List<int>> GetAvailableYearsAsync()
        {
            var yillar = await _context.LeaveRequests
                .AsNoTracking()
                .Select(l => l.StartDate.Year)
                .Distinct()
                .ToListAsync();

            var simdi = DateTime.Now.Year;
            if (!yillar.Contains(simdi)) yillar.Add(simdi);
            if (!yillar.Contains(simdi + 1)) yillar.Add(simdi + 1);

            return yillar.OrderByDescending(y => y).ToList();
        }

        // ═══════════════════════════════════════════════════════
        //  TAKVİM
        // ═══════════════════════════════════════════════════════

        public async Task<List<LeaveRequest>> GetApprovedInRangeAsync(DateTime baslangic, DateTime bitis)
        {
            var bas = baslangic.Date;
            var bit = bitis.Date;

            return await _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.User)
                .Include(l => l.Status)
                .Where(l => l.Status.Code == KOD_ONAYLANDI
                         && l.StartDate <= bit
                         && l.EndDate >= bas)
                .OrderBy(l => l.StartDate)
                .ToListAsync();
        }

        public async Task<LeaveCalendarVm> GetCalendarAsync(int yil, int ay)
        {
            if (ay < 1) { ay = 12; yil--; }
            if (ay > 12) { ay = 1; yil++; }

            var ayBasi = new DateTime(yil, ay, 1);

            // Pazartesiyle başlayan 6 tam hafta
            int kaydir = ((int)ayBasi.DayOfWeek + 6) % 7;
            var ilkGun = ayBasi.AddDays(-kaydir);
            var sonGun = ilkGun.AddDays(41);

            var izinler = await GetApprovedInRangeAsync(ilkGun, sonGun);

            var tatiller = await _context.Set<ResmiTatil>()
                .AsNoTracking()
                .Where(t => t.Tarih >= ilkGun && t.Tarih <= sonGun)
                .ToListAsync();

            var tatilMap = tatiller
                .GroupBy(t => t.Tarih.Date)
                .ToDictionary(g => g.Key, g => g.First());

            var vm = new LeaveCalendarVm { Yil = yil, Ay = ay };

            for (var d = ilkGun; d <= sonGun; d = d.AddDays(1))
            {
                tatilMap.TryGetValue(d, out var tatil);

                var gunIzinleri = izinler
                    .Where(l => l.StartDate.Date <= d && l.EndDate.Date >= d)
                    .Select(l => new LeaveCalendarEntryVm
                    {
                        RequestId = l.Id,
                        UserId = l.UserId,
                        FullName = l.User?.FullName ?? l.User?.UserName ?? "—",
                        LeaveTypeName = l.LeaveType?.Name ?? "—",
                        StartDate = l.StartDate,
                        EndDate = l.EndDate
                    })
                    .OrderBy(x => x.FullName)
                    .ToList();

                vm.Gunler.Add(new LeaveCalendarDayVm
                {
                    Tarih = d,
                    AyinIcinde = d.Month == ay && d.Year == yil,
                    Bugun = d == DateTime.Today,
                    HaftaSonu = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday,
                    TatilAdi = tatil?.Ad,
                    TatilTamGun = tatil?.TamGun ?? false,
                    Izinliler = gunIzinleri
                });
            }

            var ayIci = vm.Gunler.Where(g => g.AyinIcinde).ToList();

            vm.IzinliCalisanSayisi = ayIci.SelectMany(g => g.Izinliler)
                .Select(x => x.UserId).Distinct().Count();

            vm.ToplamIzinGunu = ayIci.Sum(g => g.Izinliler.Count);

            vm.AyinIzinleri = izinler
                .Where(l => l.StartDate <= ayBasi.AddMonths(1).AddDays(-1) && l.EndDate >= ayBasi)
                .Select(l => new LeaveCalendarEntryVm
                {
                    RequestId = l.Id,
                    UserId = l.UserId,
                    FullName = l.User?.FullName ?? l.User?.UserName ?? "—",
                    LeaveTypeName = l.LeaveType?.Name ?? "—",
                    StartDate = l.StartDate,
                    EndDate = l.EndDate
                })
                .OrderBy(x => x.StartDate)
                .ThenBy(x => x.FullName)
                .ToList();

            return vm;
        }

        // ═══════════════════════════════════════════════════════
        //  YARDIMCI
        // ═══════════════════════════════════════════════════════

        private async Task<string?> GetUserDisplayNameAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.FullName ?? u.UserName)
                .FirstOrDefaultAsync();
        }

        /// <summary>Metni sütun sınırına kırpar — uzun not SaveChanges'i patlatmasın.</summary>
        private static string? Kirp(string? s, int enFazla)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= enFazla ? s : s.Substring(0, enFazla);
        }

        /// <summary>İki aralığın kesiştiği gün sayısı.</summary>
        private static int KesisenGun(DateTime aBas, DateTime aBit, DateTime bBas, DateTime bBit)
        {
            var bas = aBas.Date > bBas.Date ? aBas.Date : bBas.Date;
            var bit = aBit.Date < bBit.Date ? aBit.Date : bBit.Date;
            return bit < bas ? 0 : (int)(bit - bas).TotalDays + 1;
        }

        // ═══════════════════════════════════════════════════════
        //  AŞAMA 1 — YÖNETİCİ ONAYI
        // ═══════════════════════════════════════════════════════

        public async Task<List<string>> GetAstIdleriAsync(string supervisorUserId)
        {
            if (string.IsNullOrWhiteSpace(supervisorUserId)) return new List<string>();

            return await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Where(x => x.SupervisorId == supervisorUserId)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> YoneticiMiAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;

            return await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .AnyAsync(x => x.SupervisorId == userId);
        }

        public async Task<int> GetEkipBekleyenSayisiAsync(string supervisorUserId)
        {
            if (string.IsNullOrWhiteSpace(supervisorUserId)) return 0;

            // Talep anındaki yönetici kayda dondurulduğu için doğrudan
            // SupervisorUserId üzerinden sayıyoruz: yönetici sonradan
            // değişse bile eski talep eski yöneticide kalır.
            return await _context.LeaveRequests
                .AsNoTracking()
                .CountAsync(l => l.SupervisorUserId == supervisorUserId
                              && l.Status.Code == KOD_BEKLIYOR);
        }

        public async Task<string?> GetYoneticiAdiAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.Supervisor.FullName ?? x.Supervisor.UserName)
                .FirstOrDefaultAsync();
        }

        public async Task<EkipIzinIndexVm> GetEkipTalepleriAsync(string supervisorUserId, int yil)
        {
            var vm = new EkipIzinIndexVm
            {
                Year = yil,
                AvailableYears = await GetAvailableYearsAsync()
            };

            var astIdler = await GetAstIdleriAsync(supervisorUserId);
            if (astIdler.Count == 0) return vm;

            // Ekip üyeleri ve bakiyeleri
            var tumBakiyeler = await GetAllBalancesAsync(yil);
            var bakiyeMap = tumBakiyeler.ToDictionary(x => x.UserId);

            vm.Ekip = astIdler
                .Select(id => bakiyeMap.TryGetValue(id, out var b) ? b : null)
                .Where(b => b != null)
                .Select(b => b!)
                .OrderBy(b => b.FullName)
                .ToList();

            // Bakiye listesi çalışan rolü süzgecinden geçtiği için bazı
            // astlar orada görünmeyebilir; onları da tek tek ekleyelim.
            var eksikler = astIdler.Where(id => !bakiyeMap.ContainsKey(id)).ToList();
            foreach (var id in eksikler)
                vm.Ekip.Add(await GetBalanceAsync(id, yil));

            vm.Ekip = vm.Ekip.OrderBy(x => x.FullName).ToList();

            // Ekibin bu yıldaki tüm talepleri
            var talepler = await _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.Status)
                .Include(l => l.User)
                .Where(l => astIdler.Contains(l.UserId) && l.StartDate.Year == yil)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            // Aynı tarihlerde izinli/izin bekleyen ekip arkadaşı sayısı
            int Cakisma(LeaveRequest t) => talepler.Count(d =>
                d.Id != t.Id
                && d.UserId != t.UserId
                && d.Status?.Code != null
                && (d.Status.Code == KOD_ONAYLANDI || ACIK_KODLAR.Contains(d.Status.Code))
                && d.StartDate.Date <= t.EndDate.Date
                && d.EndDate.Date >= t.StartDate.Date);

            EkipTalepSatirVm Satir(LeaveRequest l)
            {
                var bak = vm.Ekip.FirstOrDefault(x => x.UserId == l.UserId);

                return new EkipTalepSatirVm
                {
                    RequestId = l.Id,
                    UserId = l.UserId,
                    FullName = l.User?.FullName ?? l.User?.UserName ?? "—",
                    Title = l.User?.Title,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    LeaveTypeName = l.LeaveType?.Name ?? "—",
                    HaktanDuser = l.LeaveType?.IsDeductedFromQuota ?? false,
                    Reason = l.Reason,
                    CreatedAt = l.CreatedAt,
                    StatusCode = l.Status?.Code,
                    StatusName = l.Status?.Name,
                    SupervisorOnayladi = l.SupervisorOnayladi,
                    SupervisorNote = l.SupervisorNote,
                    SupervisorDecisionAt = l.SupervisorDecisionAt,
                    IkKarari = l.DecisionAt.HasValue
                        ? $"{l.Status?.Name} · {l.DecisionByName} · {l.DecisionAt.Value:dd.MM.yyyy}"
                        : null,
                    OnaySonrasiKalan = (l.LeaveType?.IsDeductedFromQuota == true && bak != null)
                        ? bak.RemainingDays - l.TotalDays
                        : (int?)null,
                    EkipCakismasi = Cakisma(l)
                };
            }

            vm.Bekleyenler = talepler
                .Where(l => l.Status?.Code == KOD_BEKLIYOR && l.SupervisorUserId == supervisorUserId)
                .OrderBy(l => l.StartDate)
                .Select(Satir)
                .ToList();

            var bekleyenIdler = vm.Bekleyenler.Select(x => x.RequestId).ToHashSet();

            vm.Gecmis = talepler
                .Where(l => !bekleyenIdler.Contains(l.Id))
                .Select(Satir)
                .ToList();

            vm.BugunIzinliSayisi = talepler.Count(l =>
                l.Status?.Code == KOD_ONAYLANDI
                && l.StartDate.Date <= DateTime.Today
                && l.EndDate.Date >= DateTime.Today);

            return vm;
        }

        public async Task<(bool Basarili, string? Hata)> SupervisorApproveAsync(
            int leaveRequestId, string supervisorUserId, string? not = null)
        {
            var talep = await _context.LeaveRequests
                .Include(l => l.Status)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.Id == leaveRequestId);

            if (talep == null)
                return (false, "İzin talebi bulunamadı.");

            if (talep.Status?.Code != KOD_BEKLIYOR)
                return (false, $"Bu talep yönetici onayı aşamasında değil ({talep.Status?.Name}).");

            var yetki = await YoneticiYetkisiVarMiAsync(talep, supervisorUserId);
            if (!yetki)
                return (false, "Bu talebi onaylama yetkiniz yok — çalışanın yöneticisi siz değilsiniz.");

            // Bakiye kontrolü burada SERT DEĞİL: nihai düşüm İK onayında olur.
            // Yöneticiye bilgi olarak gösteriyoruz, kararı o veriyor.

            var durum = await _context.LeaveStatuses
                .FirstOrDefaultAsync(s => s.Code == KOD_YONETICI_ONAYLADI);
            if (durum == null)
                return (false, "Sistem hatası: 'SupervisorApproved' izin durumu tanımlı değil. " +
                               "Uygulamayı yeniden başlatıp durum tanımlarının oluşmasını bekleyin.");

            talep.StatusId = durum.Id;
            talep.SupervisorOnayladi = true;
            talep.SupervisorDecisionAt = DateTime.Now;
            talep.SupervisorNote = Kirp(string.IsNullOrWhiteSpace(not) ? null : not.Trim(), 1000);

            // Kararı fiilen veren kişiyi kayda al — atanmış yönetici ile
            // onaylayan farklı olabilir (İK/Yönetici devreye girmişse).
            var onaylayanAd = await GetUserDisplayNameAsync(supervisorUserId);
            if (!string.IsNullOrEmpty(onaylayanAd))
            {
                talep.SupervisorUserId = supervisorUserId;
                talep.SupervisorName = Kirp(onaylayanAd, 200);
            }

            await _context.SaveChangesAsync();

            // Bakiye değişmedi (düşüm İK onayında) ama tutarlılık için onar
            await RecalculateBalanceAsync(talep.UserId, talep.StartDate.Year);

            return (true, null);
        }

        public async Task<(bool Basarili, string? Hata)> SupervisorRejectAsync(
            int leaveRequestId, string supervisorUserId, string gerekce)
        {
            if (string.IsNullOrWhiteSpace(gerekce))
                return (false, "Red gerekçesi zorunludur.");

            var talep = await _context.LeaveRequests
                .Include(l => l.Status)
                .FirstOrDefaultAsync(l => l.Id == leaveRequestId);

            if (talep == null)
                return (false, "İzin talebi bulunamadı.");

            if (talep.Status?.Code != KOD_BEKLIYOR)
                return (false, $"Bu talep yönetici onayı aşamasında değil ({talep.Status?.Name}).");

            var yetki = await YoneticiYetkisiVarMiAsync(talep, supervisorUserId);
            if (!yetki)
                return (false, "Bu talebi reddetme yetkiniz yok — çalışanın yöneticisi siz değilsiniz.");

            var durum = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == KOD_REDDEDILDI);
            if (durum == null)
                return (false, "Sistem hatası: 'Rejected' izin durumu tanımlı değil.");

            var ad = Kirp(await GetUserDisplayNameAsync(supervisorUserId), 200);
            var temiz = Kirp(gerekce.Trim(), 1000);

            talep.StatusId = durum.Id;
            talep.SupervisorUserId = supervisorUserId;
            talep.SupervisorName = ad;
            talep.SupervisorOnayladi = false;
            talep.SupervisorDecisionAt = DateTime.Now;
            talep.SupervisorNote = temiz;

            // Yönetici reddi nihai karardır — talep İK'ya düşmez.
            // Karar alanlarını da dolduruyoruz ki mevcut ekranlar ve izin
            // belgesi "kararı kim verdi" sorusuna cevap bulabilsin.
            talep.DecisionByUserId = supervisorUserId;
            talep.DecisionByName = ad;
            talep.DecisionAt = DateTime.Now;
            talep.DecisionNote = temiz;

            await _context.SaveChangesAsync();
            await RecalculateBalanceAsync(talep.UserId, talep.StartDate.Year);

            return (true, null);
        }

        /// <summary>
        /// Çağıran kişi bu talebi yönetici olarak karara bağlayabilir mi?
        /// Hem kayda dondurulmuş yönetici hem de güncel ast–yönetici bağı kabul edilir:
        /// talep açıldıktan sonra yönetici değiştiyse yenisi de onaylayabilir.
        /// </summary>
        private async Task<bool> YoneticiYetkisiVarMiAsync(LeaveRequest talep, string supervisorUserId)
        {
            if (string.IsNullOrWhiteSpace(supervisorUserId)) return false;
            if (talep.SupervisorUserId == supervisorUserId) return true;

            return await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .AnyAsync(x => x.UserId == talep.UserId && x.SupervisorId == supervisorUserId);
        }

        // ═══════════════════════════════════════════════════════
        //  ONAY BAĞLAMI
        // ═══════════════════════════════════════════════════════

        public async Task<OnayBaglamVm?> GetOnayBaglamiAsync(int leaveRequestId)
        {
            var talep = await GetByIdAsync(leaveRequestId);
            if (talep == null) return null;

            var yil = talep.StartDate.Year;
            var bakiye = await GetBalanceAsync(talep.UserId, yil);

            var vm = new OnayBaglamVm
            {
                Talep = talep,
                Bakiye = bakiye,
                YoneticiAdi = await GetYoneticiAdiAsync(talep.UserId),
                OnaySonrasiKalan = talep.LeaveType?.IsDeductedFromQuota == true
                    ? bakiye.RemainingDays - talep.TotalDays
                    : (int?)null
            };

            // ── Aynı tarihlerde izinli/izin bekleyen diğer çalışanlar ──
            // Önce aynı yöneticiye bağlı ekip arkadaşları, sonra aynı departman.
            var ekipIdler = new HashSet<string>();

            var yoneticiId = await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Where(x => x.UserId == talep.UserId)
                .Select(x => x.SupervisorId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(yoneticiId))
            {
                var kardesler = await GetAstIdleriAsync(yoneticiId);
                foreach (var id in kardesler) ekipIdler.Add(id);
                ekipIdler.Add(yoneticiId);
            }

            var departman = talep.User?.Departman;

            var adaylar = await _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.Status)
                .Include(l => l.User)
                .Where(l => l.UserId != talep.UserId
                         && l.StartDate <= talep.EndDate
                         && l.EndDate >= talep.StartDate
                         && (l.Status.Code == KOD_ONAYLANDI || ACIK_KODLAR.Contains(l.Status.Code)))
                .ToListAsync();

            vm.Cakismalar = adaylar
                .Where(l => ekipIdler.Contains(l.UserId)
                         || (!string.IsNullOrWhiteSpace(departman)
                             && l.User != null
                             && l.User.Departman == departman))
                .Select(l => new CakismaSatirVm
                {
                    RequestId = l.Id,
                    FullName = l.User?.FullName ?? l.User?.UserName ?? "—",
                    Title = l.User?.Title,
                    Departman = l.User?.Departman,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    LeaveTypeName = l.LeaveType?.Name ?? "—",
                    StatusCode = l.Status?.Code,
                    StatusName = l.Status?.Name,
                    AyniEkip = ekipIdler.Contains(l.UserId),
                    KesisenGun = KesisenGun(talep.StartDate, talep.EndDate, l.StartDate, l.EndDate)
                })
                .OrderByDescending(x => x.AyniEkip)
                .ThenBy(x => x.StartDate)
                .ToList();

            // ── Çalışanın bu yıldaki diğer talepleri ──
            vm.GecmisTalepler = await _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.LeaveType)
                .Include(l => l.Status)
                .Where(l => l.UserId == talep.UserId
                         && l.StartDate.Year == yil
                         && l.Id != talep.Id)
                .OrderByDescending(l => l.StartDate)
                .Select(l => new GecmisTalepVm
                {
                    RequestId = l.Id,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    LeaveTypeName = l.LeaveType.Name,
                    StatusCode = l.Status.Code,
                    StatusName = l.Status.Name
                })
                .ToListAsync();

            return vm;
        }

        // ═══════════════════════════════════════════════════════
        //  ORGANİZASYON ŞEMASI
        // ═══════════════════════════════════════════════════════

        public async Task<OrganizasyonVm> GetOrganizasyonAsync()
        {
            var calisanIdler = await GetCalisanIdleriAsync();

            // Şemada İK/yönetici hesapları da yer alabilsin: burada rol
            // süzgeci UYGULAMIYORUZ, ama pasif çalışanları işaretliyoruz.
            var kisiler = await _context.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName,
                    u.Title,
                    u.Departman,
                    u.IstenCikisTarihi
                })
                .ToListAsync();

            var baglar = await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Select(x => new { x.UserId, x.SupervisorId })
                .ToListAsync();

            // Bir çalışanın tek yöneticisi olur; veride mükerrer varsa ilkini al
            var yoneticiMap = baglar
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First().SupervisorId);

            var astMap = baglar
                .GroupBy(x => x.SupervisorId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).Distinct().ToList());

            // Açık talep sayıları — şemada rozet olarak gösterilir
            var acikTalepler = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => ACIK_KODLAR.Contains(l.Status.Code))
                .Select(l => l.UserId)
                .ToListAsync();

            var acikMap = acikTalepler
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            var kisiMap = kisiler.ToDictionary(k => k.Id);

            var vm = new OrganizasyonVm
            {
                AktifCalisanSayisi = kisiler.Count(k =>
                    k.IstenCikisTarihi == null || k.IstenCikisTarihi.Value.Date > DateTime.Today),
                YoneticiSayisi = astMap.Count
            };

            // ── Ağacı kur ──
            // Döngüye karşı korumalı: her kişi en fazla bir kez yerleştirilir.
            var yerlesenler = new HashSet<string>();

            OrgDugumVm? Dugum(string id, int seviye)
            {
                if (!kisiMap.TryGetValue(id, out var k)) return null;
                if (!yerlesenler.Add(id)) return null;   // döngü / mükerrer koruması
                if (seviye > 20) return null;            // güvenlik freni

                var d = new OrgDugumVm
                {
                    UserId = k.Id,
                    FullName = k.FullName ?? k.UserName ?? "—",
                    Title = k.Title,
                    Departman = k.Departman,
                    Aktif = k.IstenCikisTarihi == null || k.IstenCikisTarihi.Value.Date > DateTime.Today,
                    Seviye = seviye,
                    BekleyenTalepSayisi = acikMap.TryGetValue(k.Id, out var ac) ? ac : 0
                };

                if (astMap.TryGetValue(id, out var astlar))
                {
                    foreach (var astId in astlar)
                    {
                        var cocuk = Dugum(astId, seviye + 1);
                        if (cocuk != null) d.Astlar.Add(cocuk);
                    }
                    d.Astlar = d.Astlar.OrderBy(x => x.FullName).ToList();
                }

                if (seviye > vm.EnDerinSeviye) vm.EnDerinSeviye = seviye;
                return d;
            }

            // Kökler: yöneticisi olmayan AMA astı olan kişiler
            var kokIdler = astMap.Keys
                .Where(id => !yoneticiMap.ContainsKey(id))
                .OrderBy(id => kisiMap.TryGetValue(id, out var k) ? (k.FullName ?? "") : "")
                .ToList();

            foreach (var id in kokIdler)
            {
                var d = Dugum(id, 0);
                if (d != null) vm.Kokler.Add(d);
            }

            // Döngü nedeniyle hiçbir köke bağlanamamış yönetici kaldıysa
            // onu da kök olarak ekle — kimse şemadan düşmesin.
            foreach (var id in astMap.Keys.Where(x => !yerlesenler.Contains(x)).ToList())
            {
                var d = Dugum(id, 0);
                if (d != null) vm.Kokler.Add(d);
            }

            vm.Kokler = vm.Kokler.OrderByDescending(x => x.AltAgacSayisi).ThenBy(x => x.FullName).ToList();

            // ── Atama açılırı ve yöneticisizler ──
            OrgKisiVm Kisi(string id)
            {
                kisiMap.TryGetValue(id, out var k);
                yoneticiMap.TryGetValue(id, out var yonId);
                var yonAd = yonId != null && kisiMap.TryGetValue(yonId, out var y)
                    ? (y.FullName ?? y.UserName)
                    : null;

                var cikis = k?.IstenCikisTarihi;

                return new OrgKisiVm
                {
                    UserId = id,
                    FullName = k?.FullName ?? k?.UserName ?? "—",
                    Title = k?.Title,
                    Departman = k?.Departman,
                    Aktif = cikis == null || cikis.Value.Date > DateTime.Today,
                    YoneticiUserId = yonId,
                    YoneticiAdi = yonAd,
                    AstSayisi = astMap.TryGetValue(id, out var a) ? a.Count : 0
                };
            }

            // Açılırda izin takibine dahil çalışanlar + mevcut yöneticiler
            var acilirIdler = kisiler
                .Select(k => k.Id)
                .Where(id => calisanIdler == null || calisanIdler.Contains(id) || astMap.ContainsKey(id))
                .ToList();

            vm.TumCalisanlar = acilirIdler
                .Select(Kisi)
                .Where(x => x.Aktif)
                .OrderBy(x => x.FullName)
                .ToList();

            vm.Yoneticisizler = vm.TumCalisanlar
                .Where(x => string.IsNullOrEmpty(x.YoneticiUserId) && x.AstSayisi == 0)
                .OrderBy(x => x.FullName)
                .ToList();

            return vm;
        }

        public async Task<(bool Basarili, string? Hata)> SetSupervisorAsync(
            string userId, string? supervisorUserId, string atayanUserId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return (false, "Çalışan seçilmedi.");

            var varMi = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!varMi)
                return (false, "Çalışan bulunamadı.");

            var mevcutlar = await _context.Set<UserSupervisor>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            // ── Yöneticiyi kaldır ──
            if (string.IsNullOrWhiteSpace(supervisorUserId))
            {
                if (mevcutlar.Count == 0)
                    return (true, null);

                _context.Set<UserSupervisor>().RemoveRange(mevcutlar);
                await _context.SaveChangesAsync();
                return (true, null);
            }

            if (supervisorUserId == userId)
                return (false, "Bir çalışan kendi yöneticisi olamaz.");

            var yoneticiVarMi = await _context.Users.AnyAsync(u => u.Id == supervisorUserId);
            if (!yoneticiVarMi)
                return (false, "Yönetici olarak seçilen kullanıcı bulunamadı.");

            // ── Döngü kontrolü ──
            // Seçilen yönetici, bu çalışanın alt ağacında yer alıyorsa
            // atama döngü oluşturur (A → B → A) ve şema çizilemez hale gelir.
            if (await AltAgactaMiAsync(userId, supervisorUserId))
                return (false, "Bu atama organizasyon şemasında döngü oluşturur — " +
                               "seçtiğiniz kişi bu çalışanın (dolaylı) astı durumunda.");

            var ad = Kirp(await GetUserDisplayNameAsync(atayanUserId), 200);

            if (mevcutlar.Count == 1)
            {
                var kayit = mevcutlar[0];
                if (kayit.SupervisorId == supervisorUserId)
                    return (true, null);   // değişiklik yok

                kayit.SupervisorId = supervisorUserId;
                kayit.AtandiTarih = DateTime.Now;
                kayit.AtayanUserId = atayanUserId;
                kayit.AtayanAd = ad;
            }
            else
            {
                // Mükerrer ya da hiç kayıt yok — temizleyip tek kayıt yaz
                if (mevcutlar.Count > 1)
                    _context.Set<UserSupervisor>().RemoveRange(mevcutlar);

                _context.Set<UserSupervisor>().Add(new UserSupervisor
                {
                    UserId = userId,
                    SupervisorId = supervisorUserId,
                    AtandiTarih = DateTime.Now,
                    AtayanUserId = atayanUserId,
                    AtayanAd = ad
                });
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Basarili, string? Hata, int Tasinan)> AstlariDevretAsync(
            string eskiSupervisorUserId, string? yeniSupervisorUserId, string atayanUserId)
        {
            if (string.IsNullOrWhiteSpace(eskiSupervisorUserId))
                return (false, "Devredilecek yönetici seçilmedi.", 0);

            if (eskiSupervisorUserId == yeniSupervisorUserId)
                return (false, "Aynı yöneticiye devredilemez.", 0);

            var astlar = await _context.Set<UserSupervisor>()
                .Where(x => x.SupervisorId == eskiSupervisorUserId)
                .ToListAsync();

            if (astlar.Count == 0)
                return (false, "Bu yöneticiye bağlı ast bulunmuyor.", 0);

            // Yöneticiyi kaldır: astlar yöneticisiz kalır, talepleri
            // doğrudan İK onayına düşmeye başlar.
            if (string.IsNullOrWhiteSpace(yeniSupervisorUserId))
            {
                _context.Set<UserSupervisor>().RemoveRange(astlar);
                await _context.SaveChangesAsync();
                return (true, null, astlar.Count);
            }

            var ad = Kirp(await GetUserDisplayNameAsync(atayanUserId), 200);
            var tasinan = 0;
            var atlanan = new List<string>();

            foreach (var kayit in astlar)
            {
                if (kayit.UserId == yeniSupervisorUserId)
                {
                    // Yeni yönetici eski yöneticinin astıysa kendisine
                    // bağlanamaz — onu yöneticisiz bırakıp devam ediyoruz.
                    _context.Set<UserSupervisor>().Remove(kayit);
                    continue;
                }

                if (await AltAgactaMiAsync(kayit.UserId, yeniSupervisorUserId))
                {
                    atlanan.Add(kayit.UserId);
                    continue;
                }

                kayit.SupervisorId = yeniSupervisorUserId;
                kayit.AtandiTarih = DateTime.Now;
                kayit.AtayanUserId = atayanUserId;
                kayit.AtayanAd = ad;
                tasinan++;
            }

            await _context.SaveChangesAsync();

            if (atlanan.Count > 0)
                return (true, $"{tasinan} çalışan devredildi, {atlanan.Count} tanesi " +
                              "döngü oluşturacağı için atlandı.", tasinan);

            return (true, null, tasinan);
        }

        /// <summary>
        /// <paramref name="adayId"/>, <paramref name="kokId"/>'nin alt ağacında mı?
        /// Döngü kontrolü için kullanılır.
        /// </summary>
        private async Task<bool> AltAgactaMiAsync(string kokId, string adayId)
        {
            var baglar = await _context.Set<UserSupervisor>()
                .AsNoTracking()
                .Select(x => new { x.UserId, x.SupervisorId })
                .ToListAsync();

            var astMap = baglar
                .GroupBy(x => x.SupervisorId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToList());

            var kuyruk = new Queue<string>();
            var gorulen = new HashSet<string> { kokId };
            kuyruk.Enqueue(kokId);

            while (kuyruk.Count > 0)
            {
                var simdi = kuyruk.Dequeue();
                if (!astMap.TryGetValue(simdi, out var astlar)) continue;

                foreach (var ast in astlar)
                {
                    if (ast == adayId) return true;
                    if (gorulen.Add(ast)) kuyruk.Enqueue(ast);
                }
            }

            return false;
        }
    }
}