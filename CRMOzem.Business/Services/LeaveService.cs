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

        private const string KOD_BEKLIYOR = "Pending";
        private const string KOD_ONAYLANDI = "Approved";
        private const string KOD_REDDEDILDI = "Rejected";
        private const string KOD_IPTAL = "Cancelled";

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
            var bekleyen = talepler.Where(x => x.Kod == KOD_BEKLIYOR).Sum(x => x.TotalDays);

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

            var bekleyenMap = talepler.Where(x => x.Kod == KOD_BEKLIYOR)
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

            var liste = kullanicilar
                .Select(u => new PersonelSatirVm
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
                    BuYilHakedis = IzinHakedisHesaplayici.Hesapla(u.IseGirisTarihi, u.DogumTarihi, buYil)
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
                HakedisAciklamasi = IzinHakedisHesaplayici.Aciklama(u.IseGirisTarihi, u.DogumTarihi, buYil)
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
            var cakisan = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => l.UserId == request.UserId
                         && (l.Status.Code == KOD_BEKLIYOR || l.Status.Code == KOD_ONAYLANDI)
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

            var durum = await _context.LeaveStatuses.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == KOD_BEKLIYOR);
            if (durum == null)
                return (false, "Sistem hatası: 'Pending' izin durumu tanımlı değil.");

            request.StatusId = durum.Id;
            request.CreatedAt = DateTime.Now;
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

            if (talep.Status?.Code != KOD_BEKLIYOR)
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

            talep.StatusId = durum.Id;
            talep.DecisionByUserId = approverUserId;
            talep.DecisionByName = await GetUserDisplayNameAsync(approverUserId);
            talep.DecisionAt = DateTime.Now;
            talep.DecisionNote = string.IsNullOrWhiteSpace(not) ? null : not.Trim();

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

            if (talep.Status?.Code != KOD_BEKLIYOR)
                return (false, $"Bu talep zaten işlem görmüş ({talep.Status?.Name}). Tekrar reddedilemez.");

            var durum = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == KOD_REDDEDILDI);
            if (durum == null)
                return (false, "Sistem hatası: 'Rejected' izin durumu tanımlı değil.");

            talep.StatusId = durum.Id;
            talep.DecisionByUserId = approverUserId;
            talep.DecisionByName = await GetUserDisplayNameAsync(approverUserId);
            talep.DecisionAt = DateTime.Now;
            talep.DecisionNote = gerekce.Trim();

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
    }
}
