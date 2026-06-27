using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class KacAyYeterService : IKacAyYeterService
    {
        private readonly ApplicationDbContext _db;
        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public KacAyYeterService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<KacAyYeterReportVm> BuildAsync(int hospitalId, CancellationToken ct = default)
        {
            try
            {
                var hospital = await _db.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == hospitalId, ct)
                    ?? throw new InvalidOperationException("Hastane bulunamadı.");

                // Geçen ay (referans)
                var bugun = DateTime.Now;
                var gecenAy = new DateTime(bugun.Year, bugun.Month, 1).AddMonths(-1);

                // 1) Aktif testler (şablondan)
                var aktifTestler = await _db.TestTemplates.AsNoTracking()
                    .Include(t => t.Test)
                    .Where(t => t.HospitalID == hospitalId && t.IsActive)
                    .Select(t => new
                    {
                        t.TestID,
                        t.Test.TestName,
                        t.Test.SutCode,
                        t.Test.GirisTipi,
                        t.Test.KutuBasinaAdet
                    })
                    .OrderBy(t => t.TestName)
                    .ToListAsync(ct);

                if (aktifTestler.Count == 0)
                {
                    return new KacAyYeterReportVm
                    {
                        HospitalID = hospitalId,
                        HospitalName = hospital.HospitalName,
                        ReferenceMonthLabel = gecenAy.ToString("MMMM yyyy", TR),
                        Rows = new List<KacAyYeterRowVm>()
                    };
                }

                var testIds = aktifTestler.Select(t => t.TestID).ToList();

                // 2) Toplam gönderilen — TÜM ZAMANLAR
                var toplamGonderilenler = await _db.MonthlyShipments.AsNoTracking()
                    .Where(s => s.HospitalID == hospitalId && testIds.Contains(s.TestID))
                    .GroupBy(s => s.TestID)
                    .Select(g => new { TestID = g.Key, Toplam = g.Sum(x => x.SentCount) })
                    .ToDictionaryAsync(x => x.TestID, x => x.Toplam, ct);

                // 3) Toplam kullanılan — TÜM ZAMANLAR
                var toplamKullanilanlar = await _db.MonthlyStatistics.AsNoTracking()
                    .Where(s => s.HospitalID == hospitalId && testIds.Contains(s.TestID))
                    .GroupBy(s => s.TestID)
                    .Select(g => new { TestID = g.Key, Toplam = g.Sum(x => x.TestCount) })
                    .ToDictionaryAsync(x => x.TestID, x => x.Toplam, ct);

                // 4) Geçen ay tüketimi
                var gecenAyTuketim = await _db.MonthlyStatistics.AsNoTracking()
                    .Where(s => s.HospitalID == hospitalId
                                && testIds.Contains(s.TestID)
                                && s.Month == gecenAy)
                    .ToDictionaryAsync(s => s.TestID, s => s.TestCount, ct);

                // 5) Satırları oluştur
                var rows = aktifTestler.Select(t => new KacAyYeterRowVm
                {
                    TestID = t.TestID,
                    TestName = t.TestName,
                    SutCode = t.SutCode,
                    GirisTipi = t.GirisTipi,
                    KutuBasinaAdet = t.KutuBasinaAdet,
                    ToplamGonderilen = toplamGonderilenler.GetValueOrDefault(t.TestID, 0),
                    ToplamKullanilan = toplamKullanilanlar.GetValueOrDefault(t.TestID, 0),
                    GecenAyTuketim = gecenAyTuketim.GetValueOrDefault(t.TestID, 0)
                })
                // Önce kritikler, sonra uyarılar, sonra iyiler, en son veri yok
                .OrderBy(r => (int)r.Durum switch
                {
                    1 => 0,  // Kritik
                    2 => 1,  // Uyarı
                    3 => 2,  // İyi
                    _ => 3   // Veri Yok
                })
                .ThenBy(r => r.TestName)
                .ToList();

                return new KacAyYeterReportVm
                {
                    HospitalID = hospitalId,
                    HospitalName = hospital.HospitalName,
                    ReferenceMonthLabel = gecenAy.ToString("MMMM yyyy", TR),
                    Rows = rows
                };
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException("Kaç ay yeter raporu oluşturulamadı: " + ex.Message, ex);
            }
        }
    }
}