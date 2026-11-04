using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class BirimFiyatService : IBirimFiyatService
    {
        private readonly ApplicationDbContext _db;
        private readonly IKurSaglayici _kur;
        private readonly ILogger<BirimFiyatService> _log;

        public BirimFiyatService(
            ApplicationDbContext db,
            IKurSaglayici kur,
            ILogger<BirimFiyatService> log)
        {
            _db = db;
            _kur = kur;
            _log = log;
        }

        // ═══════════════════════════════════════════════════════════
        //  KLASÖRLER
        // ═══════════════════════════════════════════════════════════

        public async Task<List<BirimFiyatKlasor>> GetKlasorAgaciAsync(CancellationToken ct = default)
        {
            var hepsi = await _db.BirimFiyatKlasorleri
                .AsNoTracking()
                .Where(k => k.Aktif)
                .OrderBy(k => k.Sira).ThenBy(k => k.Ad)
                .ToListAsync(ct);

            // Karşılaştırma sayılarını tek sorguda al
            var sayilar = await _db.BirimFiyatKarsilastirmalari
                .AsNoTracking()
                .Where(x => x.KlasorId != null)
                .GroupBy(x => x.KlasorId!.Value)
                .Select(g => new { KlasorId = g.Key, Adet = g.Count() })
                .ToListAsync(ct);

            var sayiMap = sayilar.ToDictionary(x => x.KlasorId, x => x.Adet);
            var map = hepsi.ToDictionary(k => k.Id);

            foreach (var k in hepsi)
            {
                k.AltKlasorler = new List<BirimFiyatKlasor>();
                k.Karsilastirmalar = new List<BirimFiyatKarsilastirma>();
                k.KarsilastirmaAdedi = sayiMap.TryGetValue(k.Id, out var adet) ? adet : 0;
            }

            var kokler = new List<BirimFiyatKlasor>();

            foreach (var k in hepsi)
            {
                if (k.UstKlasorId.HasValue && map.TryGetValue(k.UstKlasorId.Value, out var ust))
                    ust.AltKlasorler.Add(k);
                else
                    kokler.Add(k);
            }

            // Derinlik ve alt ağaç toplamlarını hesapla
            foreach (var kok in kokler)
                AgacHesapla(kok, 0);

            return kokler;
        }

        /// <summary>Seviye ve alt ağaç dahil karşılaştırma toplamını doldurur.</summary>
        private static int AgacHesapla(BirimFiyatKlasor k, int seviye)
        {
            k.Seviye = seviye;
            var toplam = k.KarsilastirmaAdedi;

            foreach (var alt in k.AltKlasorler)
                toplam += AgacHesapla(alt, seviye + 1);

            k.ToplamKarsilastirmaAdedi = toplam;
            return toplam;
        }

        public async Task<BirimFiyatKlasor?> GetKlasorAsync(int id, CancellationToken ct = default)
        {
            return await _db.BirimFiyatKlasorleri
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == id, ct);
        }

        public async Task<(bool Basarili, string? Hata, int Id)> KlasorKaydetAsync(
            BirimFiyatKlasor klasor, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(klasor.Ad))
                return (false, "Klasör adı zorunludur.", 0);

            klasor.Ad = klasor.Ad.Trim();

            try
            {
                if (klasor.Id == 0)
                {
                    klasor.OlusturmaTarihi = DateTime.Now;
                    _db.BirimFiyatKlasorleri.Add(klasor);
                }
                else
                {
                    var mevcut = await _db.BirimFiyatKlasorleri.FirstOrDefaultAsync(k => k.Id == klasor.Id, ct);
                    if (mevcut == null) return (false, "Klasör bulunamadı.", 0);

                    // Kendi altına taşımayı engelle
                    if (klasor.UstKlasorId.HasValue)
                    {
                        if (klasor.UstKlasorId.Value == klasor.Id)
                            return (false, "Bir klasör kendi altına taşınamaz.", 0);

                        if (await AltindaMiAsync(klasor.Id, klasor.UstKlasorId.Value, ct))
                            return (false, "Bir klasör kendi alt klasörünün altına taşınamaz.", 0);
                    }

                    mevcut.Ad = klasor.Ad;
                    mevcut.UstKlasorId = klasor.UstKlasorId;
                    mevcut.Aciklama = klasor.Aciklama;
                    mevcut.Renk = klasor.Renk;
                    mevcut.Sira = klasor.Sira;
                    mevcut.GuncellemeTarihi = DateTime.Now;
                }

                await _db.SaveChangesAsync(ct);
                return (true, null, klasor.Id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Klasör kaydedilemedi.");
                return (false, EnIcMesaj(ex), 0);
            }
        }

        /// <summary>adayUstId, klasorId'nin alt ağacında mı? (döngü kontrolü)</summary>
        private async Task<bool> AltindaMiAsync(int klasorId, int adayUstId, CancellationToken ct)
        {
            var hepsi = await _db.BirimFiyatKlasorleri
                .AsNoTracking()
                .Select(k => new { k.Id, k.UstKlasorId })
                .ToListAsync(ct);

            var map = hepsi.ToDictionary(k => k.Id, k => k.UstKlasorId);

            var gecerli = adayUstId;
            var adim = 0;

            while (map.TryGetValue(gecerli, out var ust) && ust.HasValue && adim++ < 100)
            {
                if (ust.Value == klasorId) return true;
                gecerli = ust.Value;
            }

            return false;
        }

        public async Task<(bool Basarili, string? Hata)> KlasorSilAsync(int id, CancellationToken ct = default)
        {
            var klasor = await _db.BirimFiyatKlasorleri.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (klasor == null) return (false, "Klasör bulunamadı.");

            var altAdet = await _db.BirimFiyatKlasorleri.CountAsync(k => k.UstKlasorId == id, ct);
            if (altAdet > 0)
                return (false, $"Bu klasörün altında {altAdet} klasör var. Önce onları taşıyın veya silin.");

            var karsAdet = await _db.BirimFiyatKarsilastirmalari.CountAsync(k => k.KlasorId == id, ct);
            if (karsAdet > 0)
                return (false, $"Bu klasörde {karsAdet} karşılaştırma var. Önce onları taşıyın veya silin.");

            try
            {
                _db.BirimFiyatKlasorleri.Remove(klasor);
                await _db.SaveChangesAsync(ct);
                return (true, null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Klasör silinemedi. Id={Id}", id);
                return (false, EnIcMesaj(ex));
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  KARŞILAŞTIRMALAR
        // ═══════════════════════════════════════════════════════════

        public async Task<List<BirimFiyatKarsilastirma>> GetKarsilastirmalarAsync(
            int? klasorId, string? arama = null, CancellationToken ct = default)
        {
            var q = _db.BirimFiyatKarsilastirmalari
                .AsNoTracking()
                .Include(k => k.Klasor)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(arama))
            {
                var a = arama.Trim();
                q = q.Where(k =>
                    k.Ad.Contains(a) ||
                    (k.Aciklama != null && k.Aciklama.Contains(a)) ||
                    (k.IhaleAdi != null && k.IhaleAdi.Contains(a)) ||
                    (k.HastaneAdi != null && k.HastaneAdi.Contains(a)));
            }
            else
            {
                // Arama yoksa yalnızca seçili klasörün içeriği
                q = klasorId.HasValue
                    ? q.Where(k => k.KlasorId == klasorId.Value)
                    : q.Where(k => k.KlasorId == null);
            }

            return await q
                .OrderByDescending(k => k.OlusturmaTarihi)
                .ToListAsync(ct);
        }

        public async Task<BirimFiyatKarsilastirma?> GetKarsilastirmaAsync(int id, CancellationToken ct = default)
        {
            return await _db.BirimFiyatKarsilastirmalari
                .Include(k => k.Klasor)
                .FirstOrDefaultAsync(k => k.Id == id, ct);
        }

        public async Task<(bool Basarili, string? Hata, int Id)> KarsilastirmaKaydetAsync(
            BirimFiyatKarsilastirma k, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(k.Ad))
                return (false, "Karşılaştırma adı zorunludur.", 0);

            k.Ad = k.Ad.Trim();

            if (k.KurKaynagi == KurKaynagi.SabitKur && (!k.SabitKur.HasValue || k.SabitKur.Value <= 0))
                return (false, "Sabit kur seçildi ama geçerli bir kur girilmedi.", 0);

            try
            {
                if (k.Id == 0)
                {
                    k.OlusturmaTarihi = DateTime.Now;
                    k.Durum = KarsilastirmaDurumu.Taslak;
                    _db.BirimFiyatKarsilastirmalari.Add(k);
                    await _db.SaveChangesAsync(ct);
                    return (true, null, k.Id);
                }

                var mevcut = await _db.BirimFiyatKarsilastirmalari.FirstOrDefaultAsync(x => x.Id == k.Id, ct);
                if (mevcut == null) return (false, "Karşılaştırma bulunamadı.", 0);

                var kuralDegisti =
                    mevcut.EslestirmeKurali != k.EslestirmeKurali ||
                    mevcut.KurKaynagi != k.KurKaynagi ||
                    mevcut.SabitKur != k.SabitKur ||
                    mevcut.ToleransYuzde != k.ToleransYuzde;

                mevcut.Ad = k.Ad;
                mevcut.Aciklama = k.Aciklama;
                mevcut.KlasorId = k.KlasorId;
                mevcut.IhaleId = k.IhaleId;
                mevcut.IhaleAdi = k.IhaleAdi;
                mevcut.HospitalID = k.HospitalID;
                mevcut.HastaneAdi = k.HastaneAdi;
                mevcut.TedarikciEtiket = k.TedarikciEtiket;
                mevcut.BizEtiket = k.BizEtiket;
                mevcut.TedarikciParaBirimi = k.TedarikciParaBirimi;
                mevcut.BizParaBirimi = k.BizParaBirimi;
                mevcut.EslestirmeKurali = k.EslestirmeKurali;
                mevcut.KurKaynagi = k.KurKaynagi;
                mevcut.SabitKur = k.SabitKur;
                mevcut.ToleransYuzde = k.ToleransYuzde;
                mevcut.GuncellemeTarihi = DateTime.Now;

                // Kural değiştiyse eski sonuç artık geçersiz
                if (kuralDegisti && mevcut.Durum == KarsilastirmaDurumu.Hesaplandi)
                    mevcut.Durum = KarsilastirmaDurumu.DosyaBekliyor;

                await _db.SaveChangesAsync(ct);
                return (true, null, mevcut.Id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Karşılaştırma kaydedilemedi.");
                return (false, EnIcMesaj(ex), 0);
            }
        }

        public async Task<(bool Basarili, string? Hata)> KarsilastirmaSilAsync(int id, CancellationToken ct = default)
        {
            var k = await _db.BirimFiyatKarsilastirmalari.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (k == null) return (false, "Karşılaştırma bulunamadı.");

            try
            {
                await _db.BirimFiyatSonuclari.Where(s => s.KarsilastirmaId == id).ExecuteDeleteAsync(ct);
                await _db.BirimFiyatSatirlari.Where(s => s.KarsilastirmaId == id).ExecuteDeleteAsync(ct);

                _db.BirimFiyatKarsilastirmalari.Remove(k);
                await _db.SaveChangesAsync(ct);

                return (true, null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Karşılaştırma silinemedi. Id={Id}", id);
                return (false, EnIcMesaj(ex));
            }
        }

        public async Task<(bool Basarili, string? Hata)> KarsilastirmaTasiAsync(
            int id, int? hedefKlasorId, CancellationToken ct = default)
        {
            var k = await _db.BirimFiyatKarsilastirmalari.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (k == null) return (false, "Karşılaştırma bulunamadı.");

            if (hedefKlasorId.HasValue)
            {
                var varMi = await _db.BirimFiyatKlasorleri.AnyAsync(f => f.Id == hedefKlasorId.Value, ct);
                if (!varMi) return (false, "Hedef klasör bulunamadı.");
            }

            try
            {
                k.KlasorId = hedefKlasorId;
                k.GuncellemeTarihi = DateTime.Now;
                await _db.SaveChangesAsync(ct);
                return (true, null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Karşılaştırma taşınamadı. Id={Id}", id);
                return (false, EnIcMesaj(ex));
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SATIRLAR
        // ═══════════════════════════════════════════════════════════

        public async Task<(bool Basarili, string? Hata)> SatirlariKaydetAsync(
            int karsilastirmaId,
            KarsilastirmaTarafi taraf,
            string dosyaAdi,
            List<BirimFiyatSatir> satirlar,
            CancellationToken ct = default)
        {
            var k = await _db.BirimFiyatKarsilastirmalari.FirstOrDefaultAsync(x => x.Id == karsilastirmaId, ct);
            if (k == null) return (false, "Karşılaştırma bulunamadı.");

            if (satirlar == null || satirlar.Count == 0)
                return (false, "Kaydedilecek satır yok.");

            try
            {
                // Aynı tarafın önceki yüklemesini ve tüm sonuçları temizle
                await _db.BirimFiyatSatirlari
                    .Where(s => s.KarsilastirmaId == karsilastirmaId && s.Taraf == taraf)
                    .ExecuteDeleteAsync(ct);

                await _db.BirimFiyatSonuclari
                    .Where(s => s.KarsilastirmaId == karsilastirmaId)
                    .ExecuteDeleteAsync(ct);

                foreach (var s in satirlar)
                {
                    s.Id = 0;
                    s.KarsilastirmaId = karsilastirmaId;
                    s.Taraf = taraf;
                    s.OlusturmaTarihi = DateTime.Now;
                }

                _db.BirimFiyatSatirlari.AddRange(satirlar);

                if (taraf == KarsilastirmaTarafi.Tedarikci)
                {
                    k.TedarikciDosyaAdi = Kirp(dosyaAdi, 400);
                    k.TedarikciYuklemeTarihi = DateTime.Now;
                    k.TedarikciSatirSayisi = satirlar.Count;
                }
                else
                {
                    k.BizDosyaAdi = Kirp(dosyaAdi, 400);
                    k.BizYuklemeTarihi = DateTime.Now;
                    k.BizSatirSayisi = satirlar.Count;
                }

                // Sonuçlar silindiği için özet sıfırlanır
                SifirlaOzet(k);
                k.Durum = KarsilastirmaDurumu.DosyaBekliyor;
                k.GuncellemeTarihi = DateTime.Now;

                await _db.SaveChangesAsync(ct);

                _log.LogInformation("{Taraf} tarafı için {Adet} satır kaydedildi. KarsilastirmaId={Id}",
                    taraf, satirlar.Count, karsilastirmaId);

                return (true, null);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Satırlar kaydedilemedi. KarsilastirmaId={Id}", karsilastirmaId);
                return (false, EnIcMesaj(ex));
            }
        }

        public async Task<List<BirimFiyatSatir>> GetSatirlarAsync(
            int karsilastirmaId, KarsilastirmaTarafi? taraf = null, CancellationToken ct = default)
        {
            var q = _db.BirimFiyatSatirlari
                .AsNoTracking()
                .Where(s => s.KarsilastirmaId == karsilastirmaId);

            if (taraf.HasValue)
                q = q.Where(s => s.Taraf == taraf.Value);

            return await q
                .OrderBy(s => s.Taraf).ThenBy(s => s.KaynakSatirNo)
                .ToListAsync(ct);
        }

        // ═══════════════════════════════════════════════════════════
        //  HESAPLAMA
        // ═══════════════════════════════════════════════════════════

        public async Task<HesaplamaSonucu> HesaplaAsync(int karsilastirmaId, CancellationToken ct = default)
        {
            var sonuc = new HesaplamaSonucu();

            var k = await _db.BirimFiyatKarsilastirmalari.FirstOrDefaultAsync(x => x.Id == karsilastirmaId, ct);
            if (k == null)
            {
                sonuc.Hata = "Karşılaştırma bulunamadı.";
                return sonuc;
            }

            var satirlar = await _db.BirimFiyatSatirlari
                .Where(s => s.KarsilastirmaId == karsilastirmaId)
                .ToListAsync(ct);

            var tedarikci = satirlar.Where(s => s.Taraf == KarsilastirmaTarafi.Tedarikci).ToList();
            var biz = satirlar.Where(s => s.Taraf == KarsilastirmaTarafi.Biz).ToList();

            if (tedarikci.Count == 0 || biz.Count == 0)
            {
                sonuc.Hata = "Hesaplama için her iki tarafın dosyası da yüklenmiş olmalı.";
                return sonuc;
            }

            try
            {
                // 1) Kurları çöz ve her satırın TL/USD karşılığını yaz
                await KurlariUygulaAsync(k, satirlar, sonuc, ct);

                // 2) Eşleştir
                var sonucSatirlari = Eslestir(k, tedarikci, biz, sonuc);

                // 3) Yaz
                await _db.BirimFiyatSonuclari
                    .Where(s => s.KarsilastirmaId == karsilastirmaId)
                    .ExecuteDeleteAsync(ct);

                foreach (var s in sonucSatirlari)
                {
                    s.Id = 0;
                    s.KarsilastirmaId = karsilastirmaId;
                }

                _db.BirimFiyatSonuclari.AddRange(sonucSatirlari);

                // 4) Özeti güncelle
                var eslesenler = sonucSatirlari.Where(s => s.Durum == SonucDurumu.Eslesti).ToList();

                k.ToplamEslesen = eslesenler.Count;
                k.ToplamSadeceTedarikci = sonucSatirlari.Count(s => s.Durum == SonucDurumu.SadeceTedarikci);
                k.ToplamSadeceBiz = sonucSatirlari.Count(s => s.Durum == SonucDurumu.SadeceBiz);
                k.FarkliUrunSayisi = sonucSatirlari.Count(s => s.DikkatGerektiriyor);
                k.ToplamFarkTl = eslesenler.Sum(s => s.TutarEtkisiTl ?? s.FarkTl ?? 0m);
                k.ToplamFarkUsd = eslesenler.Sum(s => s.FarkUsd ?? 0m);
                k.OrtalamaSapmaYuzde = eslesenler.Count == 0
                    ? 0m
                    : Math.Round(eslesenler.Average(s => s.MutlakFarkYuzde), 4);
                k.KuruBulunamayanSatir = sonuc.KuruBulunamayan;
                k.SonHesaplamaTarihi = DateTime.Now;
                k.Durum = KarsilastirmaDurumu.Hesaplandi;
                k.GuncellemeTarihi = DateTime.Now;

                await _db.SaveChangesAsync(ct);

                sonuc.Basarili = true;
                sonuc.Eslesen = k.ToplamEslesen;
                sonuc.SadeceTedarikci = k.ToplamSadeceTedarikci;
                sonuc.SadeceBiz = k.ToplamSadeceBiz;
                sonuc.DikkatGerektiren = k.FarkliUrunSayisi;
                sonuc.ToplamFarkTl = k.ToplamFarkTl;
                sonuc.ToplamFarkUsd = k.ToplamFarkUsd;
                sonuc.OrtalamaSapmaYuzde = k.OrtalamaSapmaYuzde;

                _log.LogInformation("Karşılaştırma hesaplandı. Id={Id} · {Ozet}", karsilastirmaId, sonuc.Ozet);

                return sonuc;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Karşılaştırma hesaplanamadı. Id={Id}", karsilastirmaId);
                sonuc.Hata = EnIcMesaj(ex);
                return sonuc;
            }
        }

        // ───────────────────────────────────────────────────────────
        //  KUR UYGULAMA
        // ───────────────────────────────────────────────────────────

        private async Task KurlariUygulaAsync(
            BirimFiyatKarsilastirma k,
            List<BirimFiyatSatir> satirlar,
            HesaplamaSonucu sonuc,
            CancellationToken ct)
        {
            Dictionary<DateTime, KurSonucu>? tcmb = null;

            if (k.KurKaynagi == KurKaynagi.TcmbFaturaTarihi)
            {
                var tarihler = satirlar
                    .Where(s => s.FaturaTarihi.HasValue)
                    .Select(s => s.FaturaTarihi!.Value.Date)
                    .Distinct()
                    .ToList();

                if (tarihler.Count > 0)
                    tcmb = await _kur.GetUsdTryAsync(tarihler, ct);
            }

            // Tarihi olmayan satırlar için tek bir yedek kur
            KurSonucu? yedek = null;

            foreach (var s in satirlar)
            {
                decimal? kur = null;
                DateTime? kurTarihi = null;

                switch (k.KurKaynagi)
                {
                    case KurKaynagi.SabitKur:
                        kur = k.SabitKur;
                        kurTarihi = null;
                        break;

                    case KurKaynagi.ExcelKolonu:
                        kur = s.Kur;      // okuma sırasında dolduruldu
                        kurTarihi = s.KurTarihi;
                        break;

                    case KurKaynagi.TcmbFaturaTarihi:
                    default:
                        if (s.FaturaTarihi.HasValue && tcmb != null &&
                            tcmb.TryGetValue(s.FaturaTarihi.Value.Date, out var ks) && ks.Bulundu)
                        {
                            kur = ks.Kur;
                            kurTarihi = ks.KurTarihi;

                            if (ks.GeriyeGidildi)
                            {
                                UyariEkle(sonuc,
                                    $"{s.FaturaTarihi:dd.MM.yyyy} için bülten yok — {ks.KurTarihi:dd.MM.yyyy} kuru kullanıldı.");
                            }
                        }
                        else
                        {
                            // Tarihsiz satır: bugünün kuruna düş
                            yedek ??= await _kur.GetUsdTryAsync(DateTime.Today, ct);
                            if (yedek.Bulundu)
                            {
                                kur = yedek.Kur;
                                kurTarihi = yedek.KurTarihi;
                            }
                        }
                        break;
                }

                s.Kur = kur;
                s.KurTarihi = kurTarihi;

                if (kur == null || kur.Value <= 0)
                {
                    sonuc.KuruBulunamayan++;
                    s.BirimFiyatTl = s.ParaBirimi == "TRY" ? s.BirimFiyat : null;
                    s.BirimFiyatUsd = s.ParaBirimi == "USD" ? s.BirimFiyat : null;
                    continue;
                }

                if (string.Equals(s.ParaBirimi, "USD", StringComparison.OrdinalIgnoreCase))
                {
                    s.BirimFiyatUsd = s.BirimFiyat;
                    s.BirimFiyatTl = Math.Round(s.BirimFiyat * kur.Value, 4);
                }
                else
                {
                    s.BirimFiyatTl = s.BirimFiyat;
                    s.BirimFiyatUsd = Math.Round(s.BirimFiyat / kur.Value, 4);
                }
            }

            if (sonuc.KuruBulunamayan > 0)
            {
                UyariEkle(sonuc,
                    $"{sonuc.KuruBulunamayan} satır için kur çözülemedi; bu satırlar karşılaştırma dışı kaldı.");
            }
        }

        // ───────────────────────────────────────────────────────────
        //  EŞLEŞTİRME
        // ───────────────────────────────────────────────────────────

        private List<BirimFiyatSonuc> Eslestir(
            BirimFiyatKarsilastirma k,
            List<BirimFiyatSatir> tedarikci,
            List<BirimFiyatSatir> biz,
            HesaplamaSonucu sonuc)
        {
            if (k.EslestirmeKurali == EslestirmeKurali.ReferansVeEnYakinOnceki)
                return EslestirEnYakinOnceki(k, tedarikci, biz, sonuc);

            var tGruplar = Grupla(tedarikci, k.EslestirmeKurali);
            var bGruplar = Grupla(biz, k.EslestirmeKurali);

            var tumAnahtarlar = tGruplar.Keys.Union(bGruplar.Keys).ToList();
            var liste = new List<BirimFiyatSonuc>(tumAnahtarlar.Count);

            foreach (var anahtar in tumAnahtarlar)
            {
                tGruplar.TryGetValue(anahtar, out var tSatirlar);
                bGruplar.TryGetValue(anahtar, out var bSatirlar);

                liste.Add(SonucUret(k, anahtar.Referans, anahtar.Donem, tSatirlar, bSatirlar, sonuc));
            }

            return liste
                .OrderByDescending(s => s.DikkatGerektiriyor)
                .ThenByDescending(s => s.MutlakFarkYuzde)
                .ThenBy(s => s.UrunAdi)
                .ToList();
        }

        /// <summary>
        /// Her "biz" grubu için, aynı referansın bizim tarihimizden önceki
        /// en yakın tedarikçi fiyatını bulur.
        /// </summary>
        private List<BirimFiyatSonuc> EslestirEnYakinOnceki(
            BirimFiyatKarsilastirma k,
            List<BirimFiyatSatir> tedarikci,
            List<BirimFiyatSatir> biz,
            HesaplamaSonucu sonuc)
        {
            var liste = new List<BirimFiyatSonuc>();

            var tReferansMap = tedarikci
                .GroupBy(s => s.ReferansNormalize)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.FaturaTarihi ?? DateTime.MinValue).ToList());

            var kullanilanTedarikci = new HashSet<int>();

            var bGruplar = Grupla(biz, EslestirmeKurali.ReferansVeAy);

            foreach (var kvp in bGruplar)
            {
                var referans = kvp.Key.Referans;
                var donem = kvp.Key.Donem;
                var bSatirlar = kvp.Value;

                List<BirimFiyatSatir>? eslesen = null;

                if (tReferansMap.TryGetValue(referans, out var adaylar))
                {
                    var bizTarih = bSatirlar
                        .Where(s => s.FaturaTarihi.HasValue)
                        .Select(s => s.FaturaTarihi!.Value)
                        .DefaultIfEmpty(DateTime.MaxValue)
                        .Min();

                    var oncekiler = adaylar
                        .Where(a => a.FaturaTarihi.HasValue && a.FaturaTarihi.Value <= bizTarih)
                        .ToList();

                    // Önceki yoksa en eski tedarikçi fiyatına düş
                    var secilen = oncekiler.Count > 0
                        ? oncekiler.OrderByDescending(a => a.FaturaTarihi).First()
                        : adaylar.FirstOrDefault();

                    if (secilen != null)
                    {
                        eslesen = new List<BirimFiyatSatir> { secilen };
                        kullanilanTedarikci.Add(secilen.Id);
                    }
                }

                liste.Add(SonucUret(k, referans, donem, eslesen, bSatirlar, sonuc));
            }

            // Hiç kullanılmayan tedarikçi satırları
            var artanlar = tedarikci
                .Where(t => !kullanilanTedarikci.Contains(t.Id))
                .GroupBy(t => t.ReferansNormalize);

            foreach (var g in artanlar)
                liste.Add(SonucUret(k, g.Key, null, g.ToList(), null, sonuc));

            return liste
                .OrderByDescending(s => s.DikkatGerektiriyor)
                .ThenByDescending(s => s.MutlakFarkYuzde)
                .ThenBy(s => s.UrunAdi)
                .ToList();
        }

        private readonly record struct GrupAnahtari(string Referans, DateTime? Donem);

        private static Dictionary<GrupAnahtari, List<BirimFiyatSatir>> Grupla(
            List<BirimFiyatSatir> satirlar, EslestirmeKurali kural)
        {
            return satirlar
                .GroupBy(s => new GrupAnahtari(
                    s.ReferansNormalize,
                    kural switch
                    {
                        EslestirmeKurali.SadeceReferans => null,
                        EslestirmeKurali.ReferansVeTarih => s.FaturaTarihi?.Date,
                        _ => s.Donem
                    }))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Bir ürün/dönem çifti için sonuç satırı üretir.
        /// Aynı anahtarda birden çok satır varsa miktar ağırlıklı ortalama alınır.
        /// </summary>
        private BirimFiyatSonuc SonucUret(
            BirimFiyatKarsilastirma k,
            string referans,
            DateTime? donem,
            List<BirimFiyatSatir>? tSatirlar,
            List<BirimFiyatSatir>? bSatirlar,
            HesaplamaSonucu ozet)
        {
            var ornek = bSatirlar?.FirstOrDefault() ?? tSatirlar?.FirstOrDefault();

            var s = new BirimFiyatSonuc
            {
                ReferansNormalize = referans,
                ReferansNo = ornek?.ReferansNo,
                UrunAdi = bSatirlar?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.UrunAdi))?.UrunAdi
                          ?? tSatirlar?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.UrunAdi))?.UrunAdi,
                Donem = donem,
                TedarikciSatirAdedi = tSatirlar?.Count ?? 0,
                BizSatirAdedi = bSatirlar?.Count ?? 0
            };

            // ── Tedarikçi tarafı
            if (tSatirlar is { Count: > 0 })
            {
                var gecerli = tSatirlar.Where(x => x.BirimFiyatUsd.HasValue || x.BirimFiyatTl.HasValue).ToList();
                if (gecerli.Count > 0)
                {
                    s.TedarikciFiyatUsd = AgirlikliOrtalama(gecerli, x => x.BirimFiyatUsd);
                    s.TedarikciFiyatTl = AgirlikliOrtalama(gecerli, x => x.BirimFiyatTl);

                    var kurlu = gecerli.FirstOrDefault(x => x.Kur.HasValue);
                    s.Kur = kurlu?.Kur;
                    s.KurTarihi = kurlu?.KurTarihi;
                }

                s.TedarikciSatirId = tSatirlar[0].Id == 0 ? null : tSatirlar[0].Id;
                s.TedarikciTarih = tSatirlar.Min(x => x.FaturaTarihi);
            }

            // ── Bizim taraf
            if (bSatirlar is { Count: > 0 })
            {
                var gecerli = bSatirlar.Where(x => x.BirimFiyatTl.HasValue || x.BirimFiyatUsd.HasValue).ToList();
                if (gecerli.Count > 0)
                {
                    s.BizFiyatTl = AgirlikliOrtalama(gecerli, x => x.BirimFiyatTl);
                    s.BizFiyatUsd = AgirlikliOrtalama(gecerli, x => x.BirimFiyatUsd);

                    if (!s.Kur.HasValue)
                    {
                        var kurlu = gecerli.FirstOrDefault(x => x.Kur.HasValue);
                        s.Kur = kurlu?.Kur;
                        s.KurTarihi = kurlu?.KurTarihi;
                    }
                }

                s.BizSatirId = bSatirlar[0].Id == 0 ? null : bSatirlar[0].Id;
                s.BizTarih = bSatirlar.Min(x => x.FaturaTarihi);
                s.Miktar = bSatirlar.Sum(x => x.Miktar ?? 0m);
                if (s.Miktar == 0m) s.Miktar = null;
            }

            // ── Durum ve fark
            var tVar = s.TedarikciFiyatTl.HasValue || s.TedarikciFiyatUsd.HasValue;
            var bVar = s.BizFiyatTl.HasValue || s.BizFiyatUsd.HasValue;

            if (tVar && bVar)
            {
                s.Durum = SonucDurumu.Eslesti;

                if (s.BizFiyatTl.HasValue && s.TedarikciFiyatTl.HasValue)
                    s.FarkTl = Math.Round(s.BizFiyatTl.Value - s.TedarikciFiyatTl.Value, 4);

                if (s.BizFiyatUsd.HasValue && s.TedarikciFiyatUsd.HasValue)
                    s.FarkUsd = Math.Round(s.BizFiyatUsd.Value - s.TedarikciFiyatUsd.Value, 4);

                // Yüzde, tedarikçi fiyatı tabanlı — TL varsa TL üzerinden
                var taban = s.TedarikciFiyatTl ?? s.TedarikciFiyatUsd;
                var fark = s.TedarikciFiyatTl.HasValue ? s.FarkTl : s.FarkUsd;

                if (taban.HasValue && taban.Value != 0m && fark.HasValue)
                    s.FarkYuzde = Math.Round(fark.Value * 100m / taban.Value, 4);

                if (s.Miktar.HasValue && s.FarkTl.HasValue)
                    s.TutarEtkisiTl = Math.Round(s.FarkTl.Value * s.Miktar.Value, 2);

                var mutlak = s.MutlakFarkYuzde;

                if (mutlak <= k.ToleransYuzde)
                {
                    s.Yon = FarkYonu.Esit;
                    s.DikkatGerektiriyor = false;
                }
                else
                {
                    s.Yon = (s.FarkTl ?? s.FarkUsd ?? 0m) > 0 ? FarkYonu.BizYuksek : FarkYonu.TedarikciYuksek;
                    s.DikkatGerektiriyor = true;
                }
            }
            else if (tVar)
            {
                s.Durum = SonucDurumu.SadeceTedarikci;
                s.DikkatGerektiriyor = true;
                s.Not = "Bizim dosyada bu ürün/dönem için satır yok.";
            }
            else if (bVar)
            {
                s.Durum = SonucDurumu.SadeceBiz;
                s.DikkatGerektiriyor = true;
                s.Not = "Tedarikçi dosyasında bu ürün/dönem için satır yok.";
            }
            else
            {
                s.Durum = bSatirlar is { Count: > 0 } ? SonucDurumu.SadeceBiz : SonucDurumu.SadeceTedarikci;
                s.DikkatGerektiriyor = true;
                s.Not = "Kur çözülemediği için fiyat karşılaştırılamadı.";
            }

            if (donem == null && k.EslestirmeKurali == EslestirmeKurali.ReferansVeAy && s.Durum != SonucDurumu.Eslesti)
            {
                s.Not = string.IsNullOrEmpty(s.Not)
                    ? "Satırda fatura tarihi yok — dönem eşleştirmesi yapılamadı."
                    : s.Not + " Ayrıca fatura tarihi yok.";
            }

            return s;
        }

        /// <summary>
        /// Miktar ağırlıklı ortalama. Miktar yoksa düz ortalama alınır.
        /// Birim fiyat ortalaması için doğru olan budur.
        /// </summary>
        private static decimal? AgirlikliOrtalama(
            List<BirimFiyatSatir> satirlar,
            Func<BirimFiyatSatir, decimal?> secici)
        {
            var gecerli = satirlar.Where(s => secici(s).HasValue).ToList();
            if (gecerli.Count == 0) return null;
            if (gecerli.Count == 1) return secici(gecerli[0]);

            var toplamMiktar = gecerli.Sum(s => s.Miktar ?? 0m);

            if (toplamMiktar > 0m && gecerli.All(s => s.Miktar.HasValue && s.Miktar.Value > 0m))
            {
                var agirlikli = gecerli.Sum(s => secici(s)!.Value * s.Miktar!.Value) / toplamMiktar;
                return Math.Round(agirlikli, 4);
            }

            return Math.Round(gecerli.Average(s => secici(s)!.Value), 4);
        }

        // ═══════════════════════════════════════════════════════════
        //  SONUÇ SORGUSU
        // ═══════════════════════════════════════════════════════════

        public async Task<List<BirimFiyatSonuc>> GetSonuclarAsync(
            int karsilastirmaId,
            SonucDurumu? durum = null,
            bool sadeceDikkat = false,
            string? arama = null,
            CancellationToken ct = default)
        {
            var q = _db.BirimFiyatSonuclari
                .AsNoTracking()
                .Where(s => s.KarsilastirmaId == karsilastirmaId);

            if (durum.HasValue)
                q = q.Where(s => s.Durum == durum.Value);

            if (sadeceDikkat)
                q = q.Where(s => s.DikkatGerektiriyor);

            if (!string.IsNullOrWhiteSpace(arama))
            {
                var a = arama.Trim();
                q = q.Where(s =>
                    (s.ReferansNo != null && s.ReferansNo.Contains(a)) ||
                    (s.UrunAdi != null && s.UrunAdi.Contains(a)));
            }

            return await q
                .OrderByDescending(s => s.DikkatGerektiriyor)
                .ThenByDescending(s => s.FarkYuzde.HasValue ? Math.Abs(s.FarkYuzde.Value) : 0m)
                .ThenBy(s => s.UrunAdi)
                .ToListAsync(ct);
        }

        // ═══════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════

        private static void SifirlaOzet(BirimFiyatKarsilastirma k)
        {
            k.ToplamEslesen = 0;
            k.ToplamSadeceTedarikci = 0;
            k.ToplamSadeceBiz = 0;
            k.FarkliUrunSayisi = 0;
            k.ToplamFarkTl = 0m;
            k.ToplamFarkUsd = 0m;
            k.OrtalamaSapmaYuzde = 0m;
            k.KuruBulunamayanSatir = 0;
            k.SonHesaplamaTarihi = null;
        }

        private static void UyariEkle(HesaplamaSonucu sonuc, string mesaj)
        {
            if (sonuc.Uyarilar.Count < 50 && !sonuc.Uyarilar.Contains(mesaj))
                sonuc.Uyarilar.Add(mesaj);
        }

        private static string? Kirp(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string EnIcMesaj(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }
    }
}
