using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels.KotaTakip;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class KotaTakipService : IKotaTakipService
    {
        private readonly ApplicationDbContext _db;

        public KotaTakipService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<KotaTakipIndexViewModel> GetIndexAsync(int yil, int? ay, int? duzenlenecekKayitId = null)
        {
            yil = YiliDogrula(yil);
            ay = AyiDogrula(ay);

            var kotalar = await _db.Kotalar
                .AsNoTracking()
                .Include(x => x.KotaCariler.Where(kc => kc.Aktif))
                .ThenInclude(x => x.Cari)
                .Where(x => x.Aktif)
                .OrderBy(x => x.KotaAdi)
                .ToListAsync();

            var cariler = await _db.Carilers
                .AsNoTracking()
                .OrderBy(x => x.Unvan)
                .ToListAsync();

            var kayitSorgusu = _db.KotaFinansKayitlari
                .AsNoTracking()
                .Include(x => x.Kota)
                .Where(x => x.DonemYil == yil);

            if (ay.HasValue)
                kayitSorgusu = kayitSorgusu.Where(x => x.DonemAy == ay.Value);

            var kayitlar = await kayitSorgusu
                .OrderByDescending(x => x.FaturaTarihi)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            var giris = new KotaFinansKaydiInputViewModel
            {
                DonemYil = yil,
                DonemAy = ay ?? DateTime.Now.Month,
                FaturaTarihi = DateTime.Today,
                ParaBirimi = KotaParaBirimi.USD,
                DovizKuru = 1m
            };

            if (duzenlenecekKayitId.HasValue)
            {
                var mevcut = await _db.KotaFinansKayitlari
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == duzenlenecekKayitId.Value);

                if (mevcut != null)
                {
                    if (mevcut.KayitKaynagi != KotaKayitKaynagi.Manuel)
                        throw new InvalidOperationException("Otomatik aktarılan gelir kaydı fatura modülünden güncellenmelidir.");

                    giris = new KotaFinansKaydiInputViewModel
                    {
                        Id = mevcut.Id,
                        KotaId = mevcut.KotaId,
                        DonemYil = mevcut.DonemYil,
                        DonemAy = mevcut.DonemAy,
                        KayitTipi = mevcut.KayitTipi,
                        TedarikciGrubu = mevcut.TedarikciGrubu,
                        GiderKategorisi = mevcut.GiderKategorisi,
                        ParaBirimi = mevcut.ParaBirimi,
                        FaturaNo = mevcut.FaturaNo,
                        FaturaTarihi = mevcut.FaturaTarihi,
                        Tutar = mevcut.Tutar,
                        DovizKuru = mevcut.DovizKuru,
                        Aciklama = mevcut.Aciklama,
                        BelgeLinki = mevcut.BelgeLinki
                    };
                }
            }

            return new KotaTakipIndexViewModel
            {
                Kayit = giris,
                Kotalar = kotalar,
                Cariler = cariler,
                Kayitlar = kayitlar,
                SecilenYil = yil,
                SecilenAy = ay
            };
        }

        public async Task KayitKaydetAsync(KotaFinansKaydiInputViewModel model, string? kullaniciId)
        {
            var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.KotaId && x.Aktif);
            if (kota == null)
                throw new InvalidOperationException("Seçilen kota bulunamadı veya pasif durumda.");

            if (model.DonemYil is < 2020 or > 2100 || model.DonemAy is < 1 or > 12)
                throw new InvalidOperationException("Fatura dönemi geçerli değil.");

            var donemBaslangici = new DateTime(model.DonemYil, model.DonemAy, 1);
            var donemBitisi = donemBaslangici.AddMonths(1).AddDays(-1);
            if ((kota.BaslangicTarihi.HasValue && kota.BaslangicTarihi.Value.Date > donemBitisi) ||
                (kota.BitisTarihi.HasValue && kota.BitisTarihi.Value.Date < donemBaslangici))
                throw new InvalidOperationException("Seçilen dönem kota tarih aralığının dışında.");

            if (model.Tutar <= 0)
                throw new InvalidOperationException("Tutar sıfırdan büyük olmalıdır.");

            if (model.ParaBirimi != KotaParaBirimi.TRY && model.DovizKuru <= 0)
                throw new InvalidOperationException("Dövizli kayıtlarda kur zorunludur.");

            if (model.FaturaTarihi == default)
                throw new InvalidOperationException("Fatura tarihi zorunludur.");

            var faturaNo = (model.FaturaNo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(faturaNo))
                throw new InvalidOperationException("Fatura numarası zorunludur.");

            if (model.KayitTipi == KotaKayitTipi.Gelir)
            {
                model.TedarikciGrubu = KotaTedarikciGrubu.Ozem;
                model.GiderKategorisi = KotaGiderKategorisi.Gelir;
            }
            else if (model.GiderKategorisi == KotaGiderKategorisi.Gelir)
            {
                throw new InvalidOperationException("Gider kaydında gider kategorisi seçilmelidir.");
            }

            var tekrarVar = await _db.KotaFinansKayitlari.AnyAsync(x =>
                x.Id != model.Id &&
                x.KayitKaynagi == KotaKayitKaynagi.Manuel &&
                x.KotaId == model.KotaId &&
                x.TedarikciGrubu == model.TedarikciGrubu &&
                x.FaturaNo == faturaNo);

            if (tekrarVar)
                throw new InvalidOperationException("Aynı fatura bu kota ve tedarikçi için daha önce girilmiş.");

            KotaFinansKaydi kayit;
            if (model.Id > 0)
            {
                kayit = await _db.KotaFinansKayitlari.FirstOrDefaultAsync(x => x.Id == model.Id)
                    ?? throw new InvalidOperationException("Güncellenecek kayıt bulunamadı.");

                if (kayit.KayitKaynagi != KotaKayitKaynagi.Manuel)
                    throw new InvalidOperationException("Otomatik kayıtlar bu ekrandan değiştirilemez.");

                kayit.GuncellenmeTarihi = DateTime.Now;
            }
            else
            {
                kayit = new KotaFinansKaydi
                {
                    KayitKaynagi = KotaKayitKaynagi.Manuel,
                    OlusturanKullaniciId = kullaniciId,
                    OlusturmaTarihi = DateTime.Now
                };
                _db.KotaFinansKayitlari.Add(kayit);
            }

            kayit.KotaId = model.KotaId;
            kayit.DonemYil = model.DonemYil;
            kayit.DonemAy = model.DonemAy;
            kayit.KayitTipi = model.KayitTipi;
            kayit.TedarikciGrubu = model.TedarikciGrubu;
            kayit.GiderKategorisi = model.GiderKategorisi;
            kayit.ParaBirimi = model.ParaBirimi;
            kayit.FaturaNo = faturaNo;
            kayit.FaturaTarihi = model.FaturaTarihi.Date;
            kayit.Tutar = model.Tutar;
            kayit.DovizKuru = model.ParaBirimi == KotaParaBirimi.TRY ? 1m : model.DovizKuru;
            kayit.TutarTry = model.ParaBirimi == KotaParaBirimi.TRY
                ? decimal.Round(model.Tutar, 2)
                : decimal.Round(model.Tutar * model.DovizKuru, 2);
            kayit.Aciklama = MetniTemizle(model.Aciklama);
            kayit.BelgeLinki = MetniTemizle(model.BelgeLinki);

            await _db.SaveChangesAsync();
        }

        public async Task KayitSilAsync(int id)
        {
            var kayit = await _db.KotaFinansKayitlari.FirstOrDefaultAsync(x => x.Id == id);
            if (kayit == null)
                return;

            if (kayit.KayitKaynagi != KotaKayitKaynagi.Manuel)
                throw new InvalidOperationException("Otomatik kayıt fatura modülünden yönetilmelidir.");

            _db.KotaFinansKayitlari.Remove(kayit);
            await _db.SaveChangesAsync();
        }

        public async Task KotaEkleAsync(KotaInputViewModel model)
        {
            var kultur = CultureInfo.GetCultureInfo("tr-TR");
            var kotaKodu = (model.KotaKodu ?? string.Empty).Trim().ToUpper(kultur);
            var kotaAdi = (model.KotaAdi ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(kotaKodu) || string.IsNullOrWhiteSpace(kotaAdi))
                throw new InvalidOperationException("Kota kodu ve kota adı zorunludur.");

            if (model.ProformaUsd < 0)
                throw new InvalidOperationException("Proforma tutarı negatif olamaz.");

            if (model.BaslangicTarihi.HasValue && model.BitisTarihi.HasValue &&
                model.BitisTarihi.Value.Date < model.BaslangicTarihi.Value.Date)
                throw new InvalidOperationException("Kota bitiş tarihi başlangıç tarihinden önce olamaz.");

            if (await _db.Kotalar.AnyAsync(x => x.KotaKodu == kotaKodu))
                throw new InvalidOperationException("Bu kota kodu daha önce kullanılmış.");

            _db.Kotalar.Add(new Kota
            {
                KotaKodu = kotaKodu,
                KotaAdi = kotaAdi,
                ProformaUsd = model.ProformaUsd,
                BaslangicTarihi = model.BaslangicTarihi?.Date,
                BitisTarihi = model.BitisTarihi?.Date,
                Aktif = true,
                OlusturmaTarihi = DateTime.Now
            });

            await _db.SaveChangesAsync();
        }

        public async Task KotaCariEkleAsync(KotaCariInputViewModel model)
        {
            var kota = await _db.Kotalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.KotaId && x.Aktif);
            if (kota == null)
                throw new InvalidOperationException("Kota bulunamadı.");

            if (!await _db.Carilers.AnyAsync(x => x.Id == model.CariId))
                throw new InvalidOperationException("Cari bulunamadı.");

            var baslangic = model.BaslangicTarihi?.Date;
            var bitis = model.BitisTarihi?.Date;
            if (baslangic.HasValue && bitis.HasValue && bitis < baslangic)
                throw new InvalidOperationException("Eşleştirme bitiş tarihi başlangıç tarihinden önce olamaz.");

            if ((kota.BaslangicTarihi.HasValue && bitis.HasValue && bitis.Value < kota.BaslangicTarihi.Value.Date) ||
                (kota.BitisTarihi.HasValue && baslangic.HasValue && baslangic.Value > kota.BitisTarihi.Value.Date))
                throw new InvalidOperationException("Cari eşleştirme tarihleri kota dönemiyle kesişmiyor.");

            var mevcutlar = await _db.KotaCariler
                .Where(x => x.CariId == model.CariId && x.Aktif)
                .ToListAsync();

            if (mevcutlar.Any(x => TarihAraliklariCakisiyor(x.BaslangicTarihi, x.BitisTarihi, baslangic, bitis)))
                throw new InvalidOperationException("Bu cari aynı tarih aralığında başka bir kotaya bağlı. Önce mevcut eşleştirmeyi kaldırın.");

            _db.KotaCariler.Add(new KotaCari
            {
                KotaId = model.KotaId,
                CariId = model.CariId,
                BaslangicTarihi = baslangic,
                BitisTarihi = bitis,
                Aktif = true,
                OlusturmaTarihi = DateTime.Now
            });

            await _db.SaveChangesAsync();
        }

        public async Task KotaCariSilAsync(int id)
        {
            var eslestirme = await _db.KotaCariler.FirstOrDefaultAsync(x => x.Id == id);
            if (eslestirme == null)
                return;

            eslestirme.Aktif = false;
            await _db.SaveChangesAsync();
        }

        public async Task<KotaSenkronizasyonSonucu> GelirleriSenkronizeEtAsync(int yil, int? ay, string? kullaniciId)
        {
            yil = YiliDogrula(yil);
            ay = AyiDogrula(ay);

            var faturaSorgusu = _db.Invoices
                .AsNoTracking()
                .Where(x => x.Donemi == yil);

            if (ay.HasValue)
                faturaSorgusu = faturaSorgusu.Where(x => x.Ay == ay.Value);

            var faturalar = await faturaSorgusu.ToListAsync();
            var eslestirmeler = await _db.KotaCariler
                .AsNoTracking()
                .Include(x => x.Kota)
                .Where(x => x.Aktif && x.Kota.Aktif)
                .ToListAsync();

            var faturaIdleri = faturalar.Select(x => x.Id).ToList();
            var mevcutKayitlar = await _db.KotaFinansKayitlari
                .Where(x => x.KaynakFaturaId.HasValue && faturaIdleri.Contains(x.KaynakFaturaId.Value))
                .ToDictionaryAsync(x => x.KaynakFaturaId!.Value);

            var sonuc = new KotaSenkronizasyonSonucu();

            foreach (var fatura in faturalar)
            {
                if (fatura.Ay is < 1 or > 12)
                {
                    sonuc.Eslestirilemeyen++;
                    continue;
                }

                var donemTarihi = new DateTime(fatura.Donemi, fatura.Ay, 1);
                var donemBitisi = donemTarihi.AddMonths(1).AddDays(-1);
                var uygunEslestirmeler = eslestirmeler
                    .Where(x => x.CariId == fatura.CariId &&
                                (!x.BaslangicTarihi.HasValue || x.BaslangicTarihi.Value.Date <= donemBitisi) &&
                                (!x.BitisTarihi.HasValue || x.BitisTarihi.Value.Date >= donemTarihi) &&
                                (!x.Kota.BaslangicTarihi.HasValue || x.Kota.BaslangicTarihi.Value.Date <= donemBitisi) &&
                                (!x.Kota.BitisTarihi.HasValue || x.Kota.BitisTarihi.Value.Date >= donemTarihi))
                    .ToList();

                if (uygunEslestirmeler.Count != 1)
                {
                    if (mevcutKayitlar.TryGetValue(fatura.Id, out var gecersizKayit))
                    {
                        _db.KotaFinansKayitlari.Remove(gecersizKayit);
                        mevcutKayitlar.Remove(fatura.Id);
                        sonuc.Kaldirilan++;
                    }
                    sonuc.Eslestirilemeyen++;
                    continue;
                }

                var eslestirme = uygunEslestirmeler[0];
                if (!mevcutKayitlar.TryGetValue(fatura.Id, out var kayit))
                {
                    kayit = new KotaFinansKaydi
                    {
                        KaynakFaturaId = fatura.Id,
                        KayitKaynagi = KotaKayitKaynagi.FaturaModulu,
                        OlusturanKullaniciId = kullaniciId,
                        OlusturmaTarihi = DateTime.Now
                    };
                    _db.KotaFinansKayitlari.Add(kayit);
                    mevcutKayitlar[fatura.Id] = kayit;
                    sonuc.Eklenen++;
                }
                else
                {
                    kayit.GuncellenmeTarihi = DateTime.Now;
                    sonuc.Guncellenen++;
                }

                kayit.KotaId = eslestirme.KotaId;
                kayit.DonemYil = fatura.Donemi;
                kayit.DonemAy = fatura.Ay;
                kayit.KayitTipi = KotaKayitTipi.Gelir;
                kayit.TedarikciGrubu = KotaTedarikciGrubu.Ozem;
                kayit.GiderKategorisi = KotaGiderKategorisi.Gelir;
                kayit.ParaBirimi = KotaParaBirimi.TRY;
                kayit.FaturaNo = $"MHSB-{fatura.Id}";
                kayit.FaturaTarihi = fatura.KapanisTarihi?.Date ?? donemBitisi;
                kayit.Tutar = fatura.Tutar;
                kayit.DovizKuru = 1m;
                kayit.TutarTry = fatura.Tutar;
                kayit.Aciklama = "Muhasebe fatura modülünden otomatik aktarıldı.";
                kayit.BelgeLinki = MetniTemizle(fatura.Ekler, 500);
            }

            await _db.SaveChangesAsync();
            return sonuc;
        }

        public async Task<KotaOzetViewModel> GetOzetAsync(int yil, int? ay)
        {
            yil = YiliDogrula(yil);
            ay = AyiDogrula(ay);

            var kotalar = await _db.Kotalar
                .AsNoTracking()
                .Where(x => x.Aktif)
                .OrderBy(x => x.KotaAdi)
                .ToListAsync();

            var kayitSorgusu = _db.KotaFinansKayitlari
                .AsNoTracking()
                .Where(x => x.DonemYil == yil);

            if (ay.HasValue)
                kayitSorgusu = kayitSorgusu.Where(x => x.DonemAy == ay.Value);

            var kayitlar = await kayitSorgusu.ToListAsync();

            var satirlar = kotalar.Select(kota =>
            {
                var kotaKayitlari = kayitlar.Where(x => x.KotaId == kota.Id).ToList();
                var giderler = kotaKayitlari.Where(x => x.KayitTipi == KotaKayitTipi.Gider).ToList();
                var gelirler = kotaKayitlari.Where(x => x.KayitTipi == KotaKayitTipi.Gelir).ToList();

                return new KotaOzetSatirViewModel
                {
                    KotaId = kota.Id,
                    KotaKodu = kota.KotaKodu,
                    KotaAdi = kota.KotaAdi,
                    ProformaUsd = kota.ProformaUsd,
                    AbbottUsd = giderler
                        .Where(x => x.TedarikciGrubu == KotaTedarikciGrubu.Abbott && x.ParaBirimi == KotaParaBirimi.USD)
                        .Sum(x => x.Tutar),
                    UcuncuPartiTry = giderler
                        .Where(x => x.TedarikciGrubu == KotaTedarikciGrubu.UcuncuParti)
                        .Sum(x => x.TutarTry),
                    ToplamGiderTry = giderler.Sum(x => x.TutarTry),
                    ToplamGelirTry = gelirler.Sum(x => x.TutarTry),
                    KategoriToplamlari = giderler
                        .GroupBy(x => x.GiderKategorisi)
                        .ToDictionary(x => x.Key, x => x.Sum(y => y.TutarTry))
                };
            }).ToList();

            return new KotaOzetViewModel
            {
                SecilenYil = yil,
                SecilenAy = ay,
                Satirlar = satirlar
            };
        }

        private static int YiliDogrula(int yil) => yil is >= 2020 and <= 2100 ? yil : DateTime.Now.Year;

        private static int? AyiDogrula(int? ay) => ay is >= 1 and <= 12 ? ay : null;

        private static string? MetniTemizle(string? metin) => string.IsNullOrWhiteSpace(metin) ? null : metin.Trim();

        private static string? MetniTemizle(string? metin, int enFazlaKarakter)
        {
            var temiz = MetniTemizle(metin);
            return temiz != null && temiz.Length > enFazlaKarakter
                ? temiz[..enFazlaKarakter]
                : temiz;
        }

        private static bool TarihAraliklariCakisiyor(
            DateTime? ilkBaslangic,
            DateTime? ilkBitis,
            DateTime? ikinciBaslangic,
            DateTime? ikinciBitis)
        {
            var birinciBaslangic = ilkBaslangic ?? DateTime.MinValue;
            var birinciBitis = ilkBitis ?? DateTime.MaxValue;
            var ikinciBas = ikinciBaslangic ?? DateTime.MinValue;
            var ikinciBit = ikinciBitis ?? DateTime.MaxValue;
            return birinciBaslangic <= ikinciBit && ikinciBas <= birinciBitis;
        }
    }
}
