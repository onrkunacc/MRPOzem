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
    public class IhaleService : IIhaleService
    {
        private readonly ApplicationDbContext _db;

        public IhaleService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<Ihale>> GetAllAsync() =>
            await _db.Ihaleler.Where(i => i.Aktif).OrderByDescending(i => i.OlusturmaTarihi).ToListAsync();

        /// <summary>
        /// Liste ekranı için ihaleler + özet rakamlar.
        /// Koleksiyonları Include etmek yerine üç gruplu sorgu kullanır:
        /// yüzlerce kalemli ihalelerde liste sayfasını yavaşlatmamak için.
        /// </summary>
        public async Task<List<IhaleListeSatir>> GetListeAsync()
        {
            var ihaleler = await _db.Ihaleler
                .AsNoTracking()
                .Where(i => i.Aktif)
                .OrderByDescending(i => i.OlusturmaTarihi)
                .ToListAsync();

            if (ihaleler.Count == 0) return new List<IhaleListeSatir>();

            var idler = ihaleler.Select(i => i.Id).ToList();

            // Kalemler — ihale + kısım bazında
            var kalemGruplar = await _db.IhaleKalemler
                .AsNoTracking()
                .Where(k => idler.Contains(k.IhaleId))
                .GroupBy(k => new { k.IhaleId, k.KisimNo })
                .Select(g => new
                {
                    g.Key.IhaleId,
                    g.Key.KisimNo,
                    Adet = g.Count(),
                    TestMiktari = g.Sum(x => x.TestMiktari),
                    SutPuani = g.Sum(x => x.TestMiktari * x.SutFiyat),
                    SutFiyat = g.Sum(x => x.TestMiktari * x.SutFiyat * x.SutCarpan)
                })
                .ToListAsync();

            // Personel — ihale + kısım bazında (ToplamTRY hesaplanmış alan olduğu için formül burada)
            var personelGruplar = await _db.PersonelGiderler
                .AsNoTracking()
                .Where(p => idler.Contains(p.IhaleId))
                .Select(p => new { p.IhaleId, p.KisimNo, p.Sayi, p.BirimFiyatAylik })
                .ToListAsync();

            var ekGiderGruplar = await _db.EkGiderler
                .AsNoTracking()
                .Where(e => idler.Contains(e.IhaleId))
                .Select(e => new { e.IhaleId, e.KisimNo, e.Miktar, e.BirimFiyat, e.Kur })
                .ToListAsync();

            var liste = new List<IhaleListeSatir>(ihaleler.Count);

            foreach (var ihale in ihaleler)
            {
                var kalemler = kalemGruplar.Where(x => x.IhaleId == ihale.Id).ToList();
                var personeller = personelGruplar.Where(x => x.IhaleId == ihale.Id).ToList();
                var ekler = ekGiderGruplar.Where(x => x.IhaleId == ihale.Id).ToList();

                var personelTRY = personeller.Sum(p => p.Sayi * p.BirimFiyatAylik * ihale.IhaleSuresiGun / 30m);
                var ekGiderTRY = ekler.Sum(e => e.Miktar * e.BirimFiyat * e.Kur);
                var sutFiyat = kalemler.Sum(k => k.SutFiyat);
                var genelGider = (personelTRY + ekGiderTRY) * ihale.GenelGiderOran;

                // Hangi kısımlara veri girilmiş?
                var doluKisimlar = kalemler.Where(x => x.KisimNo.HasValue).Select(x => x.KisimNo!.Value)
                    .Concat(personeller.Where(x => x.KisimNo.HasValue).Select(x => x.KisimNo!.Value))
                    .Concat(ekler.Where(x => x.KisimNo.HasValue).Select(x => x.KisimNo!.Value))
                    .Distinct()
                    .ToList();

                var ortakAdet = kalemler.Where(x => x.KisimNo == null).Sum(x => x.Adet)
                              + personeller.Count(x => x.KisimNo == null)
                              + ekler.Count(x => x.KisimNo == null);

                liste.Add(new IhaleListeSatir
                {
                    Ihale = ihale,
                    KalemAdedi = kalemler.Sum(x => x.Adet),
                    TestMiktari = kalemler.Sum(x => x.TestMiktari),
                    ToplamSutPuani = kalemler.Sum(x => x.SutPuani),
                    ToplamSutFiyat = sutFiyat,
                    PersonelAdedi = personeller.Count,
                    PersonelTRY = personelTRY,
                    EkGiderAdedi = ekler.Count,
                    EkGiderTRY = ekGiderTRY,
                    GenelToplam = personelTRY + ekGiderTRY + genelGider + sutFiyat,
                    DoluKisimlar = doluKisimlar.OrderBy(n => n).ToList(),
                    OrtakKayitAdedi = ortakAdet
                });
            }

            return liste;
        }

        public async Task<Ihale?> GetByIdAsync(int id) =>
            await _db.Ihaleler
                .Include(i => i.PersonelGiderler).ThenInclude(p => p.Birim)
                .Include(i => i.EkGiderler).ThenInclude(e => e.Birim)
                .Include(i => i.IhaleKalemler).ThenInclude(k => k.Birim)
                .FirstOrDefaultAsync(i => i.Id == id && i.Aktif);

        public async Task<int> CreateAsync(Ihale ihale)
        {
            ihale.OlusturmaTarihi = DateTime.Now;
            if (ihale.KisimSayisi < 1) ihale.KisimSayisi = 1;
            _db.Ihaleler.Add(ihale);
            await _db.SaveChangesAsync();
            return ihale.Id;
        }

        public async Task UpdateAsync(Ihale ihale)
        {
            ihale.GuncellenmeTarihi = DateTime.Now;
            if (ihale.KisimSayisi < 1) ihale.KisimSayisi = 1;
            _db.Ihaleler.Update(ihale);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ihale = await _db.Ihaleler.FindAsync(id);
            if (ihale != null) { ihale.Aktif = false; await _db.SaveChangesAsync(); }
        }

        // ── Özet Hesaplama ────────────────────────────────────────────────────

        public async Task<OzetViewModel> GetOzetAsync(int ihaleId)
        {
            var ihale = await GetByIdAsync(ihaleId);
            if (ihale == null) throw new Exception("İhale bulunamadı.");

            var birimler = await _db.Birimler.Where(b => b.Aktif).ToListAsync();

            var personelToplam = ihale.PersonelGiderler.Sum(p => p.ToplamTRY);
            var ekGiderToplam = ihale.EkGiderler.Sum(e => e.TutarTRY);
            var toplamSUT = ihale.IhaleKalemler.Sum(k => k.ToplamSutFiyat);
            var toplamSutPuani = ihale.IhaleKalemler.Sum(k => k.ToplamSUT);

            var genelGider = (personelToplam + ekGiderToplam) * ihale.GenelGiderOran;
            var genelToplam = personelToplam + ekGiderToplam + genelGider + toplamSUT;

            // Maliyet / SUT oranı
            var sutPuani = toplamSutPuani > 0 ? genelToplam / toplamSutPuani : 0;

            // Birim bazlı özet
            var birimOzetler = birimler.Select(b => new BirimOzetSatir
            {
                BirimKodu = b.BirimKodu,
                BirimAdi = b.BirimAdi,
                PersonelTRY = ihale.PersonelGiderler.Where(p => p.BirimId == b.Id).Sum(p => p.ToplamTRY),
                EkGiderTRY = ihale.EkGiderler.Where(e => e.BirimId == b.Id).Sum(e => e.TutarTRY),
                ToplamSUT = ihale.IhaleKalemler.Where(k => k.BirimId == b.Id).Sum(k => k.ToplamSutFiyat),
                ToplamMaliyet = ihale.PersonelGiderler.Where(p => p.BirimId == b.Id).Sum(p => p.ToplamTRY)
                              + ihale.EkGiderler.Where(e => e.BirimId == b.Id).Sum(e => e.TutarTRY)
                              + ihale.IhaleKalemler.Where(k => k.BirimId == b.Id).Sum(k => k.ToplamSutFiyat)
            }).ToList();

            // ── Kısım bazlı özet ──────────────────────────────────────────
            var (kisimOzetler, ortakOzet) = KisimOzetleriOlustur(ihale, genelToplam);

            return new OzetViewModel
            {
                Ihale = ihale,
                PersonelToplam = personelToplam,
                EkGiderToplam = ekGiderToplam,
                GenelGider = genelGider,
                ToplamSutFiyat = toplamSUT,
                GenelToplam = genelToplam,
                ToplamMaliyetSutPuani = sutPuani,
                BirimOzetler = birimOzetler,
                ToplamSutPuani = toplamSutPuani,
                KisimOzetler = kisimOzetler,
                OrtakOzet = ortakOzet
            };
        }

        /// <summary>
        /// Şartnamedeki her kısım için bir özet satırı üretir.
        ///
        /// İki önemli nokta:
        /// 1) Hiçbir kısma atanmamış satırlar kısımlara DAĞITILMAZ; ayrı bir
        ///    "Ortak Giderler" satırında toplanır. Böylece her rakamın nereden
        ///    geldiği belli kalır.
        /// 2) Kısım sayısı sonradan düşürüldüğünde, aralık dışında kalan
        ///    kısım numaraları da listeye alınır ve TanimDisi olarak işaretlenir.
        ///    Aksi halde o satırların maliyeti özetten sessizce kaybolurdu.
        /// </summary>
        private static (List<KisimOzetSatir> Kisimlar, KisimOzetSatir Ortak) KisimOzetleriOlustur(
            Ihale ihale, decimal genelToplam)
        {
            var oran = ihale.GenelGiderOran;
            var tanimliSayi = Math.Max(1, ihale.KisimSayisi);

            // Veride geçen kısım numaraları + şartnamede tanımlı olanlar
            var veridekiler = ihale.IhaleKalemler.Where(k => k.KisimNo.HasValue).Select(k => k.KisimNo!.Value)
                .Concat(ihale.PersonelGiderler.Where(p => p.KisimNo.HasValue).Select(p => p.KisimNo!.Value))
                .Concat(ihale.EkGiderler.Where(e => e.KisimNo.HasValue).Select(e => e.KisimNo!.Value));

            var numaralar = Enumerable.Range(1, tanimliSayi)
                .Concat(veridekiler)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            KisimOzetSatir Olustur(int? no)
            {
                var kalemler = ihale.IhaleKalemler.Where(k => k.KisimNo == no).ToList();
                var personeller = ihale.PersonelGiderler.Where(p => p.KisimNo == no).ToList();
                var ekGiderler = ihale.EkGiderler.Where(e => e.KisimNo == no).ToList();

                var personelTRY = personeller.Sum(p => p.ToplamTRY);
                var ekGiderTRY = ekGiderler.Sum(e => e.TutarTRY);
                var sutFiyat = kalemler.Sum(k => k.ToplamSutFiyat);
                var kisimGenelGider = (personelTRY + ekGiderTRY) * oran;

                var satir = new KisimOzetSatir
                {
                    KisimNo = no,
                    TanimDisi = no.HasValue && (no.Value < 1 || no.Value > tanimliSayi),
                    PersonelTRY = personelTRY,
                    EkGiderTRY = ekGiderTRY,
                    GenelGider = kisimGenelGider,
                    ToplamSutFiyat = sutFiyat,
                    ToplamMaliyet = personelTRY + ekGiderTRY + kisimGenelGider + sutFiyat,
                    ToplamSutPuani = kalemler.Sum(k => k.ToplamSUT),
                    TestMiktari = kalemler.Sum(k => k.TestMiktari),
                    KalemAdedi = kalemler.Count,
                    PersonelAdedi = personeller.Count,
                    EkGiderAdedi = ekGiderler.Count
                };

                satir.PayYuzde = genelToplam > 0
                    ? Math.Round(satir.ToplamMaliyet / genelToplam * 100m, 2)
                    : 0m;

                return satir;
            }

            var kisimlar = numaralar.Select(n => Olustur(n)).ToList();
            var ortak = Olustur(null);

            return (kisimlar, ortak);
        }

        // ── Birimler ─────────────────────────────────────────────────────────

        public async Task<List<Birim>> GetBirimlerAsync() =>
            await _db.Birimler.Where(b => b.Aktif).OrderBy(b => b.BirimKodu).ToListAsync();

        public async Task<Birim> SaveBirimAsync(Birim birim)
        {
            if (birim.Id == 0) _db.Birimler.Add(birim);
            else _db.Birimler.Update(birim);
            await _db.SaveChangesAsync();
            return birim;
        }

        // ── Personel ─────────────────────────────────────────────────────────

        public async Task<PersonelGider> SavePersonelAsync(PersonelGider p)
        {
            NormalizeKisim(p.IhaleId, k => p.KisimNo = k, p.KisimNo);
            if (p.Id == 0) _db.PersonelGiderler.Add(p);
            else _db.PersonelGiderler.Update(p);
            await _db.SaveChangesAsync();
            return p;
        }

        public async Task DeletePersonelAsync(int id)
        {
            var p = await _db.PersonelGiderler.FindAsync(id);
            if (p != null) { _db.PersonelGiderler.Remove(p); await _db.SaveChangesAsync(); }
        }

        // ── Ek Gider ─────────────────────────────────────────────────────────

        public async Task<EkGider> SaveEkGiderAsync(EkGider e)
        {
            NormalizeKisim(e.IhaleId, k => e.KisimNo = k, e.KisimNo);
            if (e.Id == 0) _db.EkGiderler.Add(e);
            else _db.EkGiderler.Update(e);
            await _db.SaveChangesAsync();
            return e;
        }

        public async Task DeleteEkGiderAsync(int id)
        {
            var e = await _db.EkGiderler.FindAsync(id);
            if (e != null) { _db.EkGiderler.Remove(e); await _db.SaveChangesAsync(); }
        }

        // ── Kalemler ─────────────────────────────────────────────────────────

        public async Task<IhaleKalem> SaveKalemAsync(IhaleKalem k)
        {
            NormalizeKisim(k.IhaleId, v => k.KisimNo = v, k.KisimNo);
            if (k.Id == 0) _db.IhaleKalemler.Add(k);
            else _db.IhaleKalemler.Update(k);
            await _db.SaveChangesAsync();
            return k;
        }

        public async Task DeleteKalemAsync(int id)
        {
            var k = await _db.IhaleKalemler.FindAsync(id);
            if (k != null) { _db.IhaleKalemler.Remove(k); await _db.SaveChangesAsync(); }
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────

        /// <summary>
        /// 0 veya negatif kısım numarasını null'a (ortak) çevirir.
        /// Formdan boş geldiğinde JSON tarafında 0 oluşabiliyor.
        /// </summary>
        private static void NormalizeKisim(int ihaleId, Action<int?> ata, int? mevcut)
        {
            if (mevcut.HasValue && mevcut.Value < 1) ata(null);
        }
    }
}
