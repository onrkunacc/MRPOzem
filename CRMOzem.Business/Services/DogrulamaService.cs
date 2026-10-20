// ═══════════════════════════════════════════════════════════════════════
//  CRMOzem.Business/Services/DogrulamaService.cs
//
//  Day 5 — Doğrulama Ekranı iş katmanı
//  v3 — HedefDonem = DateTime? uyumu, "YYYY-MM" string'ine çevirerek
//       filtre/dropdown/VM arayüzü string tutuyor
// ═══════════════════════════════════════════════════════════════════════

using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels.Dogrulama;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class DogrulamaService : IDogrulamaService
    {
        private readonly ApplicationDbContext _db;

        public DogrulamaService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Küçük yardımcı — DateTime? → "YYYY-MM"
        private static string? DonemToStr(DateTime? d) =>
            d.HasValue ? $"{d.Value.Year:D4}-{d.Value.Month:D2}" : null;

        // ═══════════════════════════════════════════════════════════
        //  LIST
        // ═══════════════════════════════════════════════════════════
        public async Task<DogrulamaIndexVm> GetListAsync(DogrulamaFiltreVm filtre, CancellationToken ct)
        {
            var q = _db.SiparisTalepler
                .AsNoTracking()
                .Include(t => t.Hospital)
                .Include(t => t.Kalemler)
                .AsQueryable();

            if (filtre.Durum.HasValue)
                q = q.Where(t => t.Durum == filtre.Durum.Value);

            // "YYYY-MM" filtresini DateTime karşılaştırmasına çevir
            if (!string.IsNullOrWhiteSpace(filtre.Donem) &&
                DateTime.TryParseExact(filtre.Donem + "-01", "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var filtreTarih))
            {
                q = q.Where(t => t.HedefDonem.HasValue
                              && t.HedefDonem.Value.Year == filtreTarih.Year
                              && t.HedefDonem.Value.Month == filtreTarih.Month);
            }

            if (filtre.HospitalID.HasValue)
                q = q.Where(t => t.HospitalID == filtre.HospitalID.Value);

            var talepler = await q
                .OrderByDescending(t => t.TalepTarihi)
                .ToListAsync(ct);

            // Dropdown kaynakları
            var tumDonemlerRaw = await _db.SiparisTalepler
                .AsNoTracking()
                .Where(t => t.HedefDonem.HasValue)
                .Select(t => t.HedefDonem!.Value)
                .ToListAsync(ct);

            var tumDonemler = tumDonemlerRaw
                .Select(d => $"{d.Year:D4}-{d.Month:D2}")
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            var tumHastaneler = await _db.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            filtre.Donemler = tumDonemler
                .Select(d => new SelectListItem(d, d, d == filtre.Donem))
                .ToList();

            filtre.Hastaneler = tumHastaneler
                .Select(h => new SelectListItem(
                    h.HospitalName,
                    h.HospitalID.ToString(),
                    filtre.HospitalID == h.HospitalID))
                .ToList();

            // Satır VM'leri
            var satirlar = talepler.Select(t =>
            {
                var kirmizi = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Kirmizi);
                var sari = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Sari);
                var yesil = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Yesil);
                var veriYok = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.VeriYok);

                DogrulamaRengi enKotu;
                if (kirmizi > 0) enKotu = DogrulamaRengi.Kirmizi;
                else if (sari > 0) enKotu = DogrulamaRengi.Sari;
                else if (yesil > 0) enKotu = DogrulamaRengi.Yesil;
                else enKotu = DogrulamaRengi.VeriYok;

                return new DogrulamaListSatirVm
                {
                    TalepID = t.ID,
                    HospitalID = t.HospitalID,
                    HospitalName = t.Hospital?.HospitalName ?? "—",
                    HedefDonem = DonemToStr(t.HedefDonem),
                    TalepTarihi = t.TalepTarihi,
                    ExternalRefNo = t.ExternalRefNo,
                    Durum = t.Durum,
                    KalemSayisi = t.Kalemler.Count,
                    ToplamTalepTest = t.Kalemler.Sum(k => k.HastaneTalepTest),
                    ToplamOneriTest = t.Kalemler.Sum(k => k.OnerilenTest),
                    EnKotuRenk = enKotu,
                    KirmiziSayisi = kirmizi,
                    SariSayisi = sari,
                    YesilSayisi = yesil,
                    VeriYokSayisi = veriYok,
                    HastaneNotu = t.HastaneNotu
                };
            }).ToList();

            // Renge göre filtre (en kötü renk)
            if (filtre.EnKotuRenk.HasValue)
                satirlar = satirlar.Where(s => s.EnKotuRenk == filtre.EnKotuRenk.Value).ToList();

            // Üst özet rozetleri — filtreden bağımsız, GENEL duruma göre
            var genelDurumSayilari = await _db.SiparisTalepler
                .AsNoTracking()
                .GroupBy(t => t.Durum)
                .Select(g => new { Durum = g.Key, Adet = g.Count() })
                .ToListAsync(ct);

            var vm = new DogrulamaIndexVm
            {
                Satirlar = satirlar,
                Filtre = filtre,
                ToplamBekleyen = genelDurumSayilari.FirstOrDefault(x => x.Durum == SiparisTalepDurumu.Beklemede)?.Adet ?? 0,
                ToplamOnaylananAynen = genelDurumSayilari.FirstOrDefault(x => x.Durum == SiparisTalepDurumu.OnaylandiAynen)?.Adet ?? 0,
                ToplamOnaylananDuzeltildi = genelDurumSayilari.FirstOrDefault(x => x.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi)?.Adet ?? 0,
                ToplamReddedilen = genelDurumSayilari.FirstOrDefault(x => x.Durum == SiparisTalepDurumu.Reddedildi)?.Adet ?? 0,
                ToplamKirmizi = satirlar.Count(s => s.KirmiziSayisi > 0),
                ToplamSari = satirlar.Count(s => s.SariSayisi > 0 && s.KirmiziSayisi == 0)
            };

            return vm;
        }

        // ═══════════════════════════════════════════════════════════
        //  DETAY
        // ═══════════════════════════════════════════════════════════
        public async Task<DogrulamaDetayVm?> GetDetayAsync(int talepId, CancellationToken ct)
        {
            var t = await _db.SiparisTalepler
                .AsNoTracking()
                .Include(x => x.Hospital)
                .Include(x => x.Kalemler)
                .FirstOrDefaultAsync(x => x.ID == talepId, ct);

            if (t == null) return null;

            return new DogrulamaDetayVm
            {
                TalepID = t.ID,
                HospitalID = t.HospitalID,
                HospitalName = t.Hospital?.HospitalName ?? "—",
                HedefDonem = DonemToStr(t.HedefDonem),
                TalepTarihi = t.TalepTarihi,
                ExternalRefNo = t.ExternalRefNo,
                HastaneNotu = t.HastaneNotu,
                Durum = t.Durum,
                OnayTarihi = t.OnayTarihi,
                OnaylayanKullanici = t.OnaylayanKullanici,
                OnayNotu = t.OnayNotu,
                Kalemler = t.Kalemler
                    .OrderBy(k => (int)k.UrunTipi)
                    .ThenBy(k => k.ProductName)
                    .Select(k => new DogrulamaKalemVm
                    {
                        KalemID = k.ID,
                        ExternalProductId = k.ExternalProductId.ToString(),
                        ProductName = k.ProductName,
                        ReferenceNumber = k.ReferenceNumber,
                        TestID = k.TestID,
                        UrunTipi = k.UrunTipi,
                        HastaneTalepTest = k.HastaneTalepTest,
                        HastaneTalepKutu = k.HastaneTalepKutu,
                        HesapGuncelStok = k.HesapGuncelStok,
                        OrtalamaAylikKullanim = k.OrtalamaAylikKullanim,
                        OnerilenTest = k.OnerilenTest,
                        OnerilenKutu = k.OnerilenKutu,
                        SapmaYuzde = k.SapmaYuzde,
                        DogrulamaRengi = k.DogrulamaRengi,
                        HesaplamaAciklamasi = k.HesaplamaAciklamasi,
                        OnaylananTest = k.OnaylananTest ?? k.HastaneTalepTest,
                        KalemNotu = k.KalemNotu
                    })
                    .ToList()
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  ONAYLA — AYNEN
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool ok, string msg)> OnaylaAynenAsync(
            int talepId, string? onayNotu, string? kullanici, CancellationToken ct)
        {
            var t = await _db.SiparisTalepler
                .Include(x => x.Kalemler)
                .FirstOrDefaultAsync(x => x.ID == talepId, ct);

            if (t == null) return (false, "Talep bulunamadı.");
            if (t.Durum != SiparisTalepDurumu.Beklemede)
                return (false, $"Bu talep zaten '{t.Durum}' durumunda.");

            foreach (var k in t.Kalemler)
                k.OnaylananTest = k.HastaneTalepTest;

            t.Durum = SiparisTalepDurumu.OnaylandiAynen;
            t.OnayTarihi = DateTime.Now;
            t.OnaylayanKullanici = kullanici;
            t.OnayNotu = onayNotu;

            await _db.SaveChangesAsync(ct);
            return (true, "Talep aynen onaylandı.");
        }

        // ═══════════════════════════════════════════════════════════
        //  ONAYLA — DÜZELTİLMİŞ
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool ok, string msg)> OnaylaDuzeltilmisAsync(
            OnayPostDto dto, string? kullanici, CancellationToken ct)
        {
            var t = await _db.SiparisTalepler
                .Include(x => x.Kalemler)
                .FirstOrDefaultAsync(x => x.ID == dto.TalepID, ct);

            if (t == null) return (false, "Talep bulunamadı.");
            if (t.Durum != SiparisTalepDurumu.Beklemede)
                return (false, $"Bu talep zaten '{t.Durum}' durumunda.");

            var kalemMap = dto.Kalemler.ToDictionary(x => x.KalemID);
            bool herhangiDegisti = false;

            foreach (var k in t.Kalemler)
            {
                if (kalemMap.TryGetValue(k.ID, out var inp))
                {
                    var yeni = inp.OnaylananTest ?? k.HastaneTalepTest;
                    if (yeni < 0) yeni = 0;
                    if (yeni != k.HastaneTalepTest) herhangiDegisti = true;
                    k.OnaylananTest = yeni;
                    if (!string.IsNullOrWhiteSpace(inp.KalemNotu))
                        k.KalemNotu = inp.KalemNotu.Trim();
                }
                else
                {
                    k.OnaylananTest = k.HastaneTalepTest;
                }
            }

            t.Durum = herhangiDegisti
                ? SiparisTalepDurumu.OnaylandiDuzeltildi
                : SiparisTalepDurumu.OnaylandiAynen;

            t.OnayTarihi = DateTime.Now;
            t.OnaylayanKullanici = kullanici;
            t.OnayNotu = dto.OnayNotu;

            await _db.SaveChangesAsync(ct);
            return (true, herhangiDegisti
                ? "Talep düzeltilmiş olarak onaylandı."
                : "Değişiklik yoktu, talep aynen onaylandı.");
        }

        // ═══════════════════════════════════════════════════════════
        //  REDDET
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool ok, string msg)> ReddetAsync(
            int talepId, string? redNedeni, string? kullanici, CancellationToken ct)
        {
            var t = await _db.SiparisTalepler.FirstOrDefaultAsync(x => x.ID == talepId, ct);

            if (t == null) return (false, "Talep bulunamadı.");
            if (t.Durum != SiparisTalepDurumu.Beklemede)
                return (false, $"Bu talep zaten '{t.Durum}' durumunda.");

            t.Durum = SiparisTalepDurumu.Reddedildi;
            t.OnayTarihi = DateTime.Now;
            t.OnaylayanKullanici = kullanici;
            t.OnayNotu = redNedeni;

            await _db.SaveChangesAsync(ct);
            return (true, "Talep reddedildi.");
        }

        // ═══════════════════════════════════════════════════════════
        //  TOPLU ONAY (AYNEN)
        // ═══════════════════════════════════════════════════════════
        public async Task<int> TopluOnaylaAynenAsync(
            IEnumerable<int> talepIds, string? kullanici, CancellationToken ct)
        {
            var idList = talepIds.Distinct().ToList();
            if (idList.Count == 0) return 0;

            var talepler = await _db.SiparisTalepler
                .Include(x => x.Kalemler)
                .Where(x => idList.Contains(x.ID) && x.Durum == SiparisTalepDurumu.Beklemede)
                .ToListAsync(ct);

            foreach (var t in talepler)
            {
                foreach (var k in t.Kalemler)
                    k.OnaylananTest = k.HastaneTalepTest;

                t.Durum = SiparisTalepDurumu.OnaylandiAynen;
                t.OnayTarihi = DateTime.Now;
                t.OnaylayanKullanici = kullanici;
                t.OnayNotu = "Toplu onay — aynen";
            }

            await _db.SaveChangesAsync(ct);
            return talepler.Count;
        }
    }
}