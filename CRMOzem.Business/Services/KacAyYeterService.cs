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

        public async Task<KacAyYeterReportVm> BuildAsync(
            int hospitalId,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken ct = default)
        {
            try
            {
                var hospital = await _db.Hospitals.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HospitalID == hospitalId, ct)
                    ?? throw new InvalidOperationException("Hastane bulunamadı.");

                var bugun = DateTime.Now;
                var gecenAy = new DateTime(bugun.Year, bugun.Month, 1).AddMonths(-1);

                // Dönem seçili mi?
                bool periyotFiltreli = startDate.HasValue && endDate.HasValue;
                DateTime? periyotStart = null;
                DateTime? periyotEnd = null;
                int? periyotAySayisi = null;
                string referenceLabel;

                if (periyotFiltreli)
                {
                    // Ay başlarına yuvarla
                    periyotStart = new DateTime(startDate!.Value.Year, startDate.Value.Month, 1);
                    periyotEnd = new DateTime(endDate!.Value.Year, endDate.Value.Month, 1);

                    if (periyotEnd < periyotStart)
                        throw new InvalidOperationException("Bitiş tarihi başlangıçtan önce olamaz.");

                    // İnclusive ay sayısı: (yıl farkı * 12) + ay farkı + 1
                    periyotAySayisi = ((periyotEnd.Value.Year - periyotStart.Value.Year) * 12)
                                    + (periyotEnd.Value.Month - periyotStart.Value.Month) + 1;

                    referenceLabel = $"{periyotStart.Value:MMMM yyyy}  →  {periyotEnd.Value:MMMM yyyy} ({periyotAySayisi} ay)";
                }
                else
                {
                    referenceLabel = gecenAy.ToString("MMMM yyyy", TR);
                }

                // 1) Aktif testler
                var aktifTestler = await _db.TestTemplates.AsNoTracking()
                    .Include(t => t.Test)
                    .Where(t => t.HospitalID == hospitalId && t.IsActive)
                    .Select(t => new
                    {
                        t.TestID,
                        t.Test.TestName,
                        t.Test.SutCode,
                        t.Test.Kategori,
                        t.Test.Platform,
                        t.Test.GirisTipi,
                        t.Test.KutuBasinaAdet
                    })
                    .OrderBy(t => t.Platform)
                    .ThenBy(t => t.Kategori)
                    .ThenBy(t => t.TestName)
                    .ToListAsync(ct);


                if (aktifTestler.Count == 0)
                {
                    return new KacAyYeterReportVm
                    {
                        HospitalID = hospitalId,
                        HospitalName = hospital.HospitalName,
                        ReferenceMonthLabel = referenceLabel,
                        StartDate = periyotStart,
                        EndDate = periyotEnd,
                        PeriyotAySayisi = periyotAySayisi,
                        Rows = new List<KacAyYeterRowVm>()
                    };
                }

                var testIds = aktifTestler.Select(t => t.TestID).ToList();

                var shipmentQuery = _db.MonthlyShipments.AsNoTracking()
                    .Where(s => s.HospitalID == hospitalId && testIds.Contains(s.TestID));

                if (periyotFiltreli)
                {
                    shipmentQuery = shipmentQuery.Where(s =>
                        s.Month >= periyotStart!.Value && s.Month <= periyotEnd!.Value);
                }

                var toplamGonderilenler = await shipmentQuery
                    .GroupBy(s => s.TestID)
                    .Select(g => new { TestID = g.Key, Toplam = g.Sum(x => x.SentCount) })
                    .ToDictionaryAsync(x => x.TestID, x => x.Toplam, ct);

                // Toplam kullanılan (dönem filtresi ile)
                var usageQuery = _db.MonthlyStatistics.AsNoTracking()
                    .Where(s => s.HospitalID == hospitalId && testIds.Contains(s.TestID));

                if (periyotFiltreli)
                {
                    usageQuery = usageQuery.Where(s =>
                        s.Month >= periyotStart!.Value && s.Month <= periyotEnd!.Value);
                }

                var toplamKullanilanlar = await usageQuery
                    .GroupBy(s => s.TestID)
                    .Select(g => new { TestID = g.Key, Toplam = g.Sum(x => x.TestCount) })
                    .ToDictionaryAsync(x => x.TestID, x => x.Toplam, ct);

                // 4) Referans tüketim (Kaç Ay Yeter hesabı için)
                Dictionary<int, int> referansTuketim;

                if (periyotFiltreli && periyotAySayisi.GetValueOrDefault() > 0)
                {
                    // Dönem içi ortalama aylık tüketim
                    referansTuketim = toplamKullanilanlar.ToDictionary(
                        kv => kv.Key,
                        kv => (int)Math.Round((decimal)kv.Value / periyotAySayisi!.Value)
                    );
                }
                else
                {
                    // Klasik: geçen ayın tüketimi
                    referansTuketim = await _db.MonthlyStatistics.AsNoTracking()
                        .Where(s => s.HospitalID == hospitalId
                                    && testIds.Contains(s.TestID)
                                    && s.Month == gecenAy)
                        .ToDictionaryAsync(s => s.TestID, s => s.TestCount, ct);
                }

                // 5) Satırları oluştur
                var rows = aktifTestler.Select(t => new KacAyYeterRowVm
                {
                    TestID = t.TestID,
                    TestName = t.TestName,
                    SutCode = t.SutCode,
                    Kategori = t.Kategori,
                    Platform = t.Platform,
                    GirisTipi = t.GirisTipi,
                    KutuBasinaAdet = t.KutuBasinaAdet,
                    ToplamGonderilen = toplamGonderilenler.GetValueOrDefault(t.TestID, 0),
                    ToplamKullanilan = toplamKullanilanlar.GetValueOrDefault(t.TestID, 0),
                    GecenAyTuketim = referansTuketim.GetValueOrDefault(t.TestID, 0)
                })
                .OrderBy(r => (int)r.Durum switch
                {
                    1 => 0,
                    2 => 1,
                    3 => 2,
                    _ => 3
                })
                .ThenBy(r => r.TestName)
                .ToList();

                return new KacAyYeterReportVm
                {
                    HospitalID = hospitalId,
                    HospitalName = hospital.HospitalName,
                    ReferenceMonthLabel = referenceLabel,
                    StartDate = periyotStart,
                    EndDate = periyotEnd,
                    PeriyotAySayisi = periyotAySayisi,
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