using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using CRMOzem.Web.Areas.BirimFiyat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Web.Areas.BirimFiyat.Controllers
{
    [Area("BirimFiyat")]
    [Authorize(Roles = "Yönetici,Muhasebe,KotaTakip,İhale,Birim")]
    public class KarsilastirmaController : Controller
    {
        private readonly IBirimFiyatService _service;
        private readonly IBirimFiyatExcelOkuyucu _okuyucu;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<KarsilastirmaController> _log;

        /// <summary>Yükleme sihirbazı sırasında dosyanın geçici olarak tutulduğu klasör.</summary>
        private const string GeciciKlasor = "BirimFiyatTemp";

        private const long MaksDosyaBoyutu = 25 * 1024 * 1024; // 25 MB

        public KarsilastirmaController(
            IBirimFiyatService service,
            IBirimFiyatExcelOkuyucu okuyucu,
            ApplicationDbContext db,
            IWebHostEnvironment env,
            ILogger<KarsilastirmaController> log)
        {
            _service = service;
            _okuyucu = okuyucu;
            _db = db;
            _env = env;
            _log = log;
        }

        // ═══════════════════════════════════════════════════════════
        //  ANA EKRAN
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Index(int? klasorId, string? arama, CancellationToken ct)
        {
            var vm = new BirimFiyatIndexVm
            {
                AktifKlasorId = klasorId,
                Arama = arama,
                KlasorAgaci = await _service.GetKlasorAgaciAsync(ct),
                Karsilastirmalar = await _service.GetKarsilastirmalarAsync(klasorId, arama, ct)
            };

            var duzKlasorler = await _db.BirimFiyatKlasorleri
                .AsNoTracking()
                .Where(k => k.Aktif)
                .OrderBy(k => k.Ad)
                .ToListAsync(ct);

            vm.KlasorSecenekleri = KlasorSelectList(duzKlasorler, klasorId);

            if (klasorId.HasValue)
            {
                vm.AktifKlasor = duzKlasorler.FirstOrDefault(k => k.Id == klasorId.Value);
                vm.Yol = YolCikar(duzKlasorler, klasorId.Value);
            }

            // Üst şerit özetleri — tüm modül geneli
            var tumu = await _db.BirimFiyatKarsilastirmalari
                .AsNoTracking()
                .Select(k => new { k.Durum, k.FarkliUrunSayisi, k.ToplamFarkTl })
                .ToListAsync(ct);

            vm.ToplamKarsilastirma = tumu.Count;
            vm.HesaplananSayisi = tumu.Count(x => x.Durum == KarsilastirmaDurumu.Hesaplandi);
            vm.DikkatGerektirenToplam = tumu.Sum(x => x.FarkliUrunSayisi);
            vm.ToplamFarkTl = tumu.Sum(x => x.ToplamFarkTl);

            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  KLASÖR İŞLEMLERİ (AJAX)
        // ═══════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KlasorKaydet(
            int id, string ad, int? ustKlasorId, string? aciklama, string? renk, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ad))
                return Json(new { success = false, message = "Klasör adı zorunludur." });

            var klasor = new BirimFiyatKlasor
            {
                Id = id,
                Ad = ad,
                UstKlasorId = ustKlasorId,
                Aciklama = aciklama,
                Renk = renk,
                OlusturanKullanici = User.Identity?.Name
            };

            var (basarili, hata, yeniId) = await _service.KlasorKaydetAsync(klasor, ct);

            return Json(new
            {
                success = basarili,
                message = basarili ? (id == 0 ? "Klasör oluşturuldu." : "Klasör güncellendi.") : hata,
                id = yeniId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KlasorSil(int id, CancellationToken ct)
        {
            var (basarili, hata) = await _service.KlasorSilAsync(id, ct);
            return Json(new { success = basarili, message = basarili ? "Klasör silindi." : hata });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Tasi(int id, int? hedefKlasorId, CancellationToken ct)
        {
            var (basarili, hata) = await _service.KarsilastirmaTasiAsync(id, hedefKlasorId, ct);
            return Json(new { success = basarili, message = basarili ? "Taşındı." : hata });
        }

        // ═══════════════════════════════════════════════════════════
        //  KARŞILAŞTIRMA OLUŞTUR / DÜZENLE
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Olustur(int? klasorId, CancellationToken ct)
        {
            var vm = new KarsilastirmaFormVm
            {
                Karsilastirma = new BirimFiyatKarsilastirma
                {
                    KlasorId = klasorId,
                    EslestirmeKurali = EslestirmeKurali.ReferansVeAy,
                    KurKaynagi = KurKaynagi.TcmbFaturaTarihi,
                    TedarikciEtiket = "Abbott",
                    BizEtiket = "ÖZEM",
                    TedarikciParaBirimi = "USD",
                    BizParaBirimi = "TRY",
                    ToleransYuzde = 1m
                }
            };

            await FormuDoldurAsync(vm, ct);
            return View("Form", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            if (k == null) return NotFound();

            var vm = new KarsilastirmaFormVm { Karsilastirma = k };
            await FormuDoldurAsync(vm, ct);

            return View("Form", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kaydet(BirimFiyatKarsilastirma karsilastirma, CancellationToken ct)
        {
            // Ondalık alanlar: sunucu kültürü ile tarayıcının gönderdiği biçim
            // farklı olabildiği için ham form değerini kültürden bağımsız çöz.
            OndalikDuzelt("Karsilastirma.SabitKur", v => karsilastirma.SabitKur = v);
            OndalikDuzelt("Karsilastirma.ToleransYuzde", v => { if (v.HasValue) karsilastirma.ToleransYuzde = v.Value; });

            ModelState.Remove(nameof(karsilastirma.SabitKur));
            ModelState.Remove(nameof(karsilastirma.ToleransYuzde));
            ModelState.Remove("Karsilastirma.SabitKur");
            ModelState.Remove("Karsilastirma.ToleransYuzde");

            if (!ModelState.IsValid)
            {
                var hataliVm = new KarsilastirmaFormVm { Karsilastirma = karsilastirma };
                await FormuDoldurAsync(hataliVm, ct);
                return View("Form", hataliVm);
            }

            if (karsilastirma.Id == 0)
                karsilastirma.OlusturanKullanici = User.Identity?.Name;

            // Hastane adını anlık görüntü olarak sakla
            if (karsilastirma.HospitalID.HasValue)
            {
                karsilastirma.HastaneAdi = await _db.Hospitals
                    .Where(h => h.HospitalID == karsilastirma.HospitalID.Value)
                    .Select(h => h.HospitalName)
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                karsilastirma.HastaneAdi = null;
            }

            var (basarili, hata, id) = await _service.KarsilastirmaKaydetAsync(karsilastirma, ct);

            if (!basarili)
            {
                ModelState.AddModelError(string.Empty, hata ?? "Kaydedilemedi.");
                var hataliVm = new KarsilastirmaFormVm { Karsilastirma = karsilastirma };
                await FormuDoldurAsync(hataliVm, ct);
                return View("Form", hataliVm);
            }

            TempData["SuccessMessage"] = "Karşılaştırma kaydedildi.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            var klasorId = k?.KlasorId;

            var (basarili, hata) = await _service.KarsilastirmaSilAsync(id, ct);

            if (basarili)
            {
                GeciciDosyalariTemizle(id);
                TempData["SuccessMessage"] = "Karşılaştırma silindi.";
            }
            else
            {
                TempData["Error"] = hata;
            }

            return RedirectToAction(nameof(Index), new { klasorId });
        }

        // ═══════════════════════════════════════════════════════════
        //  DETAY / SONUÇLAR
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Detay(
            int id, SonucDurumu? durum, bool sadeceDikkat, string? arama, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            if (k == null) return NotFound();

            var vm = new KarsilastirmaDetayVm
            {
                Karsilastirma = k,
                Durum = durum,
                SadeceDikkat = sadeceDikkat,
                Arama = arama
            };

            if (k.Durum == KarsilastirmaDurumu.Hesaplandi)
            {
                vm.Sonuclar = await _service.GetSonuclarAsync(id, durum, sadeceDikkat, arama, ct);

                // Dönem özetleri — filtreden bağımsız, tüm sonuçlar üzerinden
                var hepsi = (durum == null && !sadeceDikkat && string.IsNullOrWhiteSpace(arama))
                    ? vm.Sonuclar
                    : await _service.GetSonuclarAsync(id, null, false, null, ct);

                vm.DonemOzetleri = hepsi
                    .Where(s => s.Durum == SonucDurumu.Eslesti)
                    .GroupBy(s => s.Donem)
                    .Select(g => new DonemOzetVm
                    {
                        Donem = g.Key,
                        Adet = g.Count(),
                        DikkatAdedi = g.Count(x => x.DikkatGerektiriyor),
                        ToplamFarkTl = g.Sum(x => x.TutarEtkisiTl ?? x.FarkTl ?? 0m),
                        OrtalamaSapmaYuzde = Math.Round(g.Average(x => x.MutlakFarkYuzde), 2)
                    })
                    .OrderBy(x => x.Donem ?? DateTime.MaxValue)
                    .ToList();

                vm.EnBuyukSapmalar = hepsi
                    .Where(s => s.Durum == SonucDurumu.Eslesti && s.DikkatGerektiriyor)
                    .OrderByDescending(s => s.MutlakFarkYuzde)
                    .Take(5)
                    .ToList();
            }

            if (k.KuruBulunamayanSatir > 0)
                vm.Uyarilar.Add($"{k.KuruBulunamayanSatir} satır için kur çözülemedi — bu satırlar karşılaştırmaya girmedi.");

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hesapla(int id, CancellationToken ct)
        {
            var sonuc = await _service.HesaplaAsync(id, ct);

            if (sonuc.Basarili)
            {
                TempData["SuccessMessage"] = "Karşılaştırma hesaplandı · " + sonuc.Ozet;

                if (sonuc.Uyarilar.Count > 0)
                    TempData["Warning"] = string.Join(" · ", sonuc.Uyarilar.Take(3));
            }
            else
            {
                TempData["Error"] = "Hesaplanamadı: " + sonuc.Hata;
            }

            return RedirectToAction(nameof(Detay), new { id });
        }

        // ═══════════════════════════════════════════════════════════
        //  YÜKLEME SİHİRBAZI
        // ═══════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaksDosyaBoyutu)]
        public async Task<IActionResult> DosyaYukle(
            int id, KarsilastirmaTarafi taraf, IFormFile? dosya, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            if (k == null) return NotFound();

            if (dosya == null || dosya.Length == 0)
            {
                TempData["Error"] = "Dosya seçilmedi.";
                return RedirectToAction(nameof(Detay), new { id });
            }

            if (dosya.Length > MaksDosyaBoyutu)
            {
                TempData["Error"] = "Dosya 25 MB sınırını aşıyor.";
                return RedirectToAction(nameof(Detay), new { id });
            }

            var uzanti = Path.GetExtension(dosya.FileName).ToLowerInvariant();
            if (uzanti != ".xlsx" && uzanti != ".xlsm")
            {
                TempData["Error"] = "Yalnızca .xlsx dosyası yüklenebilir. Eski .xls dosyalarını Excel'de 'Farklı Kaydet → .xlsx' ile çevirin.";
                return RedirectToAction(nameof(Detay), new { id });
            }

            try
            {
                var yol = GeciciDosyaYolu(id, taraf);
                Directory.CreateDirectory(Path.GetDirectoryName(yol)!);

                await using (var fs = new FileStream(yol, FileMode.Create, FileAccess.Write))
                {
                    await dosya.CopyToAsync(fs, ct);
                }

                // Orijinal dosya adını eşleştirme adımına taşı
                TempData["YuklenenDosyaAdi_" + (int)taraf] = dosya.FileName;

                return RedirectToAction(nameof(Eslestir), new { id, taraf });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Dosya yüklenemedi. KarsilastirmaId={Id} Taraf={Taraf}", id, taraf);
                TempData["Error"] = "Dosya yüklenemedi: " + EnIcMesaj(ex);
                return RedirectToAction(nameof(Detay), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Eslestir(
            int id, KarsilastirmaTarafi taraf, string? sayfa, int? baslikSatiri, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            if (k == null) return NotFound();

            var yol = GeciciDosyaYolu(id, taraf);
            if (!System.IO.File.Exists(yol))
            {
                TempData["Error"] = "Yüklenen dosya bulunamadı, lütfen tekrar yükleyin.";
                return RedirectToAction(nameof(Detay), new { id });
            }

            var dosyaAdi = TempData.Peek("YuklenenDosyaAdi_" + (int)taraf) as string
                           ?? (taraf == KarsilastirmaTarafi.Tedarikci ? k.TedarikciDosyaAdi : k.BizDosyaAdi)
                           ?? "Yüklenen dosya";

            await using var fs = new FileStream(yol, FileMode.Open, FileAccess.Read);

            var onizleme = _okuyucu.Onizle(fs, dosyaAdi, sayfa, baslikSatiri ?? 1);

            var vm = new YuklemeVm
            {
                Karsilastirma = k,
                Taraf = taraf,
                Onizleme = onizleme,
                Eslesme = onizleme.Tahmin
            };

            if (!onizleme.Basarili)
                TempData["Error"] = onizleme.Hata;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EslestirOnayla(EslestirmeOnayVm form, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(form.KarsilastirmaId, ct);
            if (k == null) return NotFound();

            var yol = GeciciDosyaYolu(form.KarsilastirmaId, form.Taraf);
            if (!System.IO.File.Exists(yol))
            {
                TempData["Error"] = "Yüklenen dosya bulunamadı, lütfen tekrar yükleyin.";
                return RedirectToAction(nameof(Detay), new { id = form.KarsilastirmaId });
            }

            var eslesme = form.ToDto();

            if (!eslesme.Gecerli)
            {
                TempData["Error"] = "Referans ve birim fiyat kolonlarını seçmelisiniz.";
                return RedirectToAction(nameof(Eslestir),
                    new { id = form.KarsilastirmaId, taraf = form.Taraf, sayfa = form.SayfaAdi, baslikSatiri = form.BaslikSatiri });
            }

            var paraBirimi = form.Taraf == KarsilastirmaTarafi.Tedarikci
                ? k.TedarikciParaBirimi
                : k.BizParaBirimi;

            // Kur kolonu seçilmediyse ve kaynak "Excel kolonu" ise uyar
            if (k.KurKaynagi == KurKaynagi.ExcelKolonu && eslesme.KurKolon < 0 && paraBirimi == "USD")
            {
                TempData["Warning"] = "Kur kaynağı 'Excel kolonu' seçili ama kur kolonu eşleştirilmedi.";
            }

            ExcelOkumaSonucu okuma;
            List<BirimFiyatSatir> satirlar;

            await using (var fs = new FileStream(yol, FileMode.Open, FileAccess.Read))
            {
                okuma = _okuyucu.Oku(fs, eslesme, form.Taraf, paraBirimi, out satirlar);
            }

            if (!okuma.Basarili)
            {
                TempData["Error"] = okuma.Hata;
                return RedirectToAction(nameof(Eslestir),
                    new { id = form.KarsilastirmaId, taraf = form.Taraf, sayfa = form.SayfaAdi, baslikSatiri = form.BaslikSatiri });
            }

            var dosyaAdi = TempData["YuklenenDosyaAdi_" + (int)form.Taraf] as string ?? "Yüklenen dosya";

            var (basarili, hata) = await _service.SatirlariKaydetAsync(
                form.KarsilastirmaId, form.Taraf, dosyaAdi, satirlar, ct);

            if (!basarili)
            {
                TempData["Error"] = "Satırlar kaydedilemedi: " + hata;
                return RedirectToAction(nameof(Detay), new { id = form.KarsilastirmaId });
            }

            // Geçici dosya artık gereksiz
            TryDelete(yol);

            var mesaj = $"{okuma.OkunanSatir} satır alındı.";
            if (okuma.AtlananSatir > 0) mesaj += $" {okuma.AtlananSatir} satır atlandı.";
            TempData["SuccessMessage"] = mesaj;

            if (okuma.Uyarilar.Count > 0)
                TempData["Warning"] = string.Join(" · ", okuma.Uyarilar.Take(3));

            return RedirectToAction(nameof(Detay), new { id = form.KarsilastirmaId });
        }

        // ═══════════════════════════════════════════════════════════
        //  HAM SATIRLAR
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> Satirlar(int id, KarsilastirmaTarafi taraf, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            if (k == null) return NotFound();

            var satirlar = await _service.GetSatirlarAsync(id, taraf, ct);

            ViewBag.Karsilastirma = k;
            ViewBag.Taraf = taraf;
            ViewBag.TarafAdi = taraf == KarsilastirmaTarafi.Tedarikci ? k.TedarikciEtiket : k.BizEtiket;

            return View(satirlar);
        }

        // ═══════════════════════════════════════════════════════════
        //  EXCEL ÇIKTISI
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> SonucExcel(int id, CancellationToken ct)
        {
            var k = await _service.GetKarsilastirmaAsync(id, ct);
            if (k == null) return NotFound();

            var sonuclar = await _service.GetSonuclarAsync(id, null, false, null, ct);

            using var wb = new XLWorkbook();

            // ── Özet sayfası
            var ozet = wb.Worksheets.Add("Özet");
            var satir = 1;

            ozet.Cell(satir, 1).Value = k.Ad;
            ozet.Cell(satir, 1).Style.Font.Bold = true;
            ozet.Cell(satir, 1).Style.Font.FontSize = 14;
            satir += 2;

            void OzetSatir(string etiket, string deger)
            {
                ozet.Cell(satir, 1).Value = etiket;
                ozet.Cell(satir, 1).Style.Font.Bold = true;
                ozet.Cell(satir, 2).Value = deger;
                satir++;
            }

            OzetSatir("Tedarikçi", $"{k.TedarikciEtiket} ({k.TedarikciParaBirimi})");
            OzetSatir("Bizim taraf", $"{k.BizEtiket} ({k.BizParaBirimi})");
            OzetSatir("Eşleştirme kuralı", KuralAdi(k.EslestirmeKurali));
            OzetSatir("Kur kaynağı", KurKaynakAdi(k.KurKaynagi, k.SabitKur));
            OzetSatir("Tolerans", k.ToleransYuzde.ToString("N2") + " %");
            if (!string.IsNullOrWhiteSpace(k.IhaleAdi)) OzetSatir("İhale", k.IhaleAdi);
            if (!string.IsNullOrWhiteSpace(k.HastaneAdi)) OzetSatir("Hastane", k.HastaneAdi);
            satir++;

            OzetSatir("Eşleşen satır", k.ToplamEslesen.ToString("N0"));
            OzetSatir($"Sadece {k.TedarikciEtiket}'ta", k.ToplamSadeceTedarikci.ToString("N0"));
            OzetSatir($"Sadece {k.BizEtiket}'de", k.ToplamSadeceBiz.ToString("N0"));
            OzetSatir("Dikkat gerektiren", k.FarkliUrunSayisi.ToString("N0"));
            OzetSatir("Toplam fark (TL)", k.ToplamFarkTl.ToString("N2"));
            OzetSatir("Toplam fark (USD)", k.ToplamFarkUsd.ToString("N2"));
            OzetSatir("Ortalama sapma", k.OrtalamaSapmaYuzde.ToString("N2") + " %");
            OzetSatir("Hesaplama tarihi", k.SonHesaplamaTarihi?.ToString("dd.MM.yyyy HH:mm") ?? "-");

            ozet.Columns(1, 2).AdjustToContents();

            // ── Detay sayfası
            var ws = wb.Worksheets.Add("Karşılaştırma");

            var basliklar = new[]
            {
                "Referans", "Ürün Adı", "Dönem",
                $"{k.TedarikciEtiket} (USD)", $"{k.TedarikciEtiket} (TL)",
                $"{k.BizEtiket} (TL)", $"{k.BizEtiket} (USD)",
                "Kur", "Fark (TL)", "Fark (USD)", "Fark (%)",
                "Miktar", "Tutar Etkisi (TL)", "Durum", "Dikkat", "Not"
            };

            for (var i = 0; i < basliklar.Length; i++)
            {
                var hucre = ws.Cell(1, i + 1);
                hucre.Value = basliklar[i];
                hucre.Style.Font.Bold = true;
                hucre.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
                hucre.Style.Font.FontColor = XLColor.White;
            }

            var r = 2;
            foreach (var s in sonuclar)
            {
                ws.Cell(r, 1).Value = s.ReferansNo ?? s.ReferansNormalize;
                ws.Cell(r, 2).Value = s.UrunAdi ?? "";
                ws.Cell(r, 3).Value = s.Donem?.ToString("MM.yyyy") ?? "";

                if (s.TedarikciFiyatUsd.HasValue) ws.Cell(r, 4).Value = s.TedarikciFiyatUsd.Value;
                if (s.TedarikciFiyatTl.HasValue) ws.Cell(r, 5).Value = s.TedarikciFiyatTl.Value;
                if (s.BizFiyatTl.HasValue) ws.Cell(r, 6).Value = s.BizFiyatTl.Value;
                if (s.BizFiyatUsd.HasValue) ws.Cell(r, 7).Value = s.BizFiyatUsd.Value;
                if (s.Kur.HasValue) ws.Cell(r, 8).Value = s.Kur.Value;
                if (s.FarkTl.HasValue) ws.Cell(r, 9).Value = s.FarkTl.Value;
                if (s.FarkUsd.HasValue) ws.Cell(r, 10).Value = s.FarkUsd.Value;
                if (s.FarkYuzde.HasValue) ws.Cell(r, 11).Value = s.FarkYuzde.Value;
                if (s.Miktar.HasValue) ws.Cell(r, 12).Value = s.Miktar.Value;
                if (s.TutarEtkisiTl.HasValue) ws.Cell(r, 13).Value = s.TutarEtkisiTl.Value;

                ws.Cell(r, 14).Value = DurumAdi(s.Durum, k);
                ws.Cell(r, 15).Value = s.DikkatGerektiriyor ? "EVET" : "";
                ws.Cell(r, 16).Value = s.Not ?? "";

                if (s.DikkatGerektiriyor)
                    ws.Range(r, 1, r, basliklar.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#fef2f2");

                r++;
            }

            ws.Columns(4, 13).Style.NumberFormat.Format = "#,##0.0000";
            ws.Column(11).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(13).Style.NumberFormat.Format = "#,##0.00";
            ws.SheetView.FreezeRows(1);
            ws.Columns(1, basliklar.Length).AdjustToContents();
            ws.RangeUsed()?.SetAutoFilter();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var ad = $"{Temizle(k.Ad)}_karsilastirma_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ad);
        }

        // ═══════════════════════════════════════════════════════════
        //  ŞABLON İNDİRME
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Kullanıcının doldurup yükleyeceği Excel şablonu.
        /// Başlık adları okuyucunun otomatik kolon tespitiyle birebir uyumludur —
        /// bu şablonla yüklenen dosyada eşleştirme adımı kendiliğinden doğru gelir.
        /// id/taraf verilirse açıklama sayfası o tarafa göre özelleşir.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SablonIndir(int? id, KarsilastirmaTarafi? taraf, CancellationToken ct)
        {
            BirimFiyatKarsilastirma? k = null;
            if (id.HasValue)
                k = await _service.GetKarsilastirmaAsync(id.Value, ct);

            var tarafAdi = "—";
            var paraBirimi = "—";

            if (k != null && taraf.HasValue)
            {
                tarafAdi = taraf.Value == KarsilastirmaTarafi.Tedarikci ? k.TedarikciEtiket : k.BizEtiket;
                paraBirimi = taraf.Value == KarsilastirmaTarafi.Tedarikci ? k.TedarikciParaBirimi : k.BizParaBirimi;
            }

            using var wb = new XLWorkbook();

            // ═══ 1) VERİ sayfası — kullanıcı burayı doldurur ═══
            var ws = wb.Worksheets.Add("Veri");

            var kolonlar = new (string Baslik, double Genislik, string? Bicim, bool Zorunlu)[]
            {
                ("Referans No",   18, "@",          true),
                ("Ürün Adı",      46, null,         false),
                ("Fatura Tarihi", 15, "dd.mm.yyyy", false),
                ("Birim Fiyat",   15, "#,##0.0000", true),
                ("Miktar",        12, "#,##0.00",   false),
                ("Birim",         11, null,         false),
                ("Fatura No",     18, "@",          false),
                ("Kur",           12, "#,##0.0000", false)
            };

            for (var i = 0; i < kolonlar.Length; i++)
            {
                var kol = kolonlar[i];
                var hucre = ws.Cell(1, i + 1);

                hucre.Value = kol.Baslik;
                hucre.Style.Font.Bold = true;
                hucre.Style.Font.FontColor = XLColor.White;
                hucre.Style.Fill.BackgroundColor = kol.Zorunlu
                    ? XLColor.FromHtml("#4338ca")   // zorunlu → indigo
                    : XLColor.FromHtml("#1e293b");  // opsiyonel → lacivert
                hucre.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hucre.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                ws.Column(i + 1).Width = kol.Genislik;

                // Veri aralığına biçim uygula — yapıştırma sonrası da geçerli olsun
                if (kol.Bicim != null)
                    ws.Range(2, i + 1, 2000, i + 1).Style.NumberFormat.Format = kol.Bicim;
            }

            ws.Row(1).Height = 22;
            ws.SheetView.FreezeRows(1);
            ws.Range(1, 1, 1, kolonlar.Length).SetAutoFilter();

            // 2. satır: silinmek üzere bırakılan sarı örnek satır
            var ornekSatir = new[]
            {
                "07D8225", "Architect Troponin-I Reagent Kit", "15.01.2026",
                "128,4500", "40", "Kutu", "ABB2026000145", ""
            };

            for (var i = 0; i < ornekSatir.Length; i++)
            {
                var h = ws.Cell(2, i + 1);
                h.Value = ornekSatir[i];
                h.Style.Font.Italic = true;
                h.Style.Font.FontColor = XLColor.FromHtml("#94a3b8");
                h.Style.Fill.BackgroundColor = XLColor.FromHtml("#fffbeb");
            }

            // ═══ 2) AÇIKLAMA sayfası ═══
            var yrd = wb.Worksheets.Add("Nasıl Doldurulur");
            var s = 1;

            void Baslik(string metin, int boyut = 13)
            {
                yrd.Cell(s, 1).Value = metin;
                yrd.Cell(s, 1).Style.Font.Bold = true;
                yrd.Cell(s, 1).Style.Font.FontSize = boyut;
                yrd.Cell(s, 1).Style.Font.FontColor = XLColor.FromHtml("#1e293b");
                s += 2;
            }

            void Satir(string sol, string sag)
            {
                yrd.Cell(s, 1).Value = sol;
                yrd.Cell(s, 1).Style.Font.Bold = true;
                yrd.Cell(s, 2).Value = sag;
                yrd.Cell(s, 2).Style.Alignment.WrapText = true;
                s++;
            }

            Baslik("Birim Fiyat Karşılaştırma — Veri Şablonu", 15);

            if (k != null)
            {
                Satir("Karşılaştırma", k.Ad);
                if (taraf.HasValue)
                {
                    Satir("Bu dosya kimin?", tarafAdi);
                    Satir("Para birimi", paraBirimi + " — birim fiyatları bu para biriminde yazın");
                }
                s++;
            }

            Baslik("Kolonlar");

            Satir("Referans No *", "Ürün kodu / katalog numarası. İki dosyanın eşleştiği anahtar budur. " +
                                   "Büyük-küçük harf, baştaki sıfırlar ve tire/boşluk farkı önemsenmez: " +
                                   "07D8225 ile 7d8225 aynı ürün sayılır.");
            Satir("Ürün Adı", "Raporda okunabilirlik için. Eşleştirmede kullanılmaz, boş bırakılabilir.");
            Satir("Fatura Tarihi", "Faturanın kesildiği tarih. Dönem eşleştirmesi ve TCMB kuru buna göre belirlenir. " +
                                   "GG.AA.YYYY biçiminde yazın (örn. 15.01.2026). Boş bırakılırsa satır " +
                                   "dönem eşleştirmesine giremez.");
            Satir("Birim Fiyat *", "Bir adedin fiyatı — toplam tutar DEĞİL. " +
                                   (paraBirimi == "—" ? "Dosyanın para biriminde." : paraBirimi + " cinsinden."));
            Satir("Miktar", "Varsa yazın; farkın toplam tutar etkisi bununla hesaplanır. Zorunlu değil.");
            Satir("Birim", "Adet, kutu, test vb. Bilgi amaçlıdır.");
            Satir("Fatura No", "Geri izleme için saklanır. Zorunlu değil.");
            Satir("Kur", "Yalnızca karşılaştırma ayarlarında kur kaynağı 'Excel kolonu' seçiliyse doldurun. " +
                         "TCMB seçiliyse boş bırakın — sistem fatura tarihinden kendisi çeker.");

            s++;
            Baslik("Kurallar");

            var kurallar = new[]
            {
                "Mor başlıklı iki kolon (Referans No, Birim Fiyat) zorunludur; bunlar boşsa satır atlanır.",
                "Başlık satırını (1. satır) silmeyin, adlarını değiştirmeyin — sistem kolonları bu adlardan tanıyor.",
                "2. satırdaki sarı örnek satırı silin, kendi verinizi 2. satırdan itibaren yazın.",
                "Ondalık ayracı olarak virgül de nokta da kabul edilir: 128,45 ile 128.45 aynı okunur.",
                "Aynı ürünün aynı ay içinde birden fazla satırı olabilir — sistem miktar ağırlıklı ortalamasını alır.",
                "Dosyayı .xlsx olarak kaydedin. Eski .xls biçimi okunamaz.",
                "Satır sayısı sınırı yok; 2000. satırdan sonrası için hazır biçim yok ama veri yine okunur."
            };

            foreach (var kural in kurallar)
            {
                yrd.Cell(s, 1).Value = "•";
                yrd.Cell(s, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                yrd.Cell(s, 2).Value = kural;
                yrd.Cell(s, 2).Style.Alignment.WrapText = true;
                s++;
            }

            s++;
            Baslik("Örnek satırlar");

            var ornekBaslikSatiri = s;
            for (var i = 0; i < kolonlar.Length; i++)
            {
                var h = yrd.Cell(ornekBaslikSatiri, i + 1);
                h.Value = kolonlar[i].Baslik;
                h.Style.Font.Bold = true;
                h.Style.Font.FontColor = XLColor.White;
                h.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            }

            var ornekler = new[]
            {
                new[] { "07D8225", "Architect Troponin-I Reagent Kit", "15.01.2026", "128,4500", "40", "Kutu", "ABB2026000145", "" },
                new[] { "3P2525",  "Alinity HIV Ag/Ab Combo",          "15.01.2026", "96,0000",  "25", "Kutu", "ABB2026000145", "" },
                new[] { "08P5490", "Alinity Ferritin Calibrator",      "02.02.2026", "412,7500", "6",  "Kutu", "ABB2026000211", "" }
            };

            var os = ornekBaslikSatiri + 1;
            foreach (var satir in ornekler)
            {
                for (var i = 0; i < satir.Length; i++)
                    yrd.Cell(os, i + 1).Value = satir[i];
                os++;
            }

            yrd.Column(1).Width = 17;
            yrd.Column(2).Width = 86;
            for (var i = 3; i <= kolonlar.Length; i++)
                yrd.Column(i).Width = 15;

            // Dosya açılınca Veri sayfası görünsün
            ws.SetTabActive();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var dosyaAdi = (k != null && taraf.HasValue)
                ? $"birim-fiyat-sablon_{Temizle(tarafAdi)}.xlsx"
                : "birim-fiyat-sablon.xlsx";

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", dosyaAdi);
        }

        // ═══════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Form alanındaki ham metni kültürden bağımsız çözüp hedefe yazar.
        /// Alan formda yoksa hiçbir şey yapmaz.
        /// </summary>
        private void OndalikDuzelt(string alanAdi, Action<decimal?> ata)
        {
            if (!Request.HasFormContentType) return;
            if (!Request.Form.ContainsKey(alanAdi)) return;

            var ham = Request.Form[alanAdi].ToString();
            if (string.IsNullOrWhiteSpace(ham))
            {
                ata(null);
                return;
            }

            var deger = CRMOzem.Business.Services.BirimFiyatExcelOkuyucu.MetniSayiyaCevir(ham);
            if (deger.HasValue) ata(deger);
        }

        private async Task FormuDoldurAsync(KarsilastirmaFormVm vm, CancellationToken ct)
        {
            var klasorler = await _db.BirimFiyatKlasorleri
                .AsNoTracking()
                .Where(k => k.Aktif)
                .OrderBy(k => k.Ad)
                .ToListAsync(ct);

            vm.Klasorler = KlasorSelectList(klasorler, vm.Karsilastirma.KlasorId);

            vm.Hastaneler = await _db.Hospitals
                .AsNoTracking()
                .OrderBy(h => h.HospitalName)
                .Select(h => new SelectListItem
                {
                    Value = h.HospitalID.ToString(),
                    Text = h.HospitalName,
                    Selected = vm.Karsilastirma.HospitalID == h.HospitalID
                })
                .ToListAsync(ct);

            vm.Hastaneler.Insert(0, new SelectListItem("— Hastane seçilmedi —", ""));
        }

        /// <summary>Klasörleri girintili tek liste haline getirir.</summary>
        private static List<SelectListItem> KlasorSelectList(List<BirimFiyatKlasor> hepsi, int? secili)
        {
            var liste = new List<SelectListItem>
            {
                new SelectListItem("— Kök (klasörsüz) —", "", secili == null)
            };

            var cocukMap = hepsi
                .GroupBy(k => k.UstKlasorId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sira).ThenBy(x => x.Ad).ToList());

            void Ekle(int ustId, int seviye)
            {
                if (!cocukMap.TryGetValue(ustId, out var cocuklar)) return;

                foreach (var c in cocuklar)
                {
                    liste.Add(new SelectListItem(
                        new string(' ', seviye * 4) + (seviye > 0 ? "└ " : "") + c.Ad,
                        c.Id.ToString(),
                        secili == c.Id));

                    if (seviye < 8) Ekle(c.Id, seviye + 1);
                }
            }

            Ekle(0, 0);
            return liste;
        }

        /// <summary>Kökten verilen klasöre kadar olan yolu çıkarır.</summary>
        private static List<BirimFiyatKlasor> YolCikar(List<BirimFiyatKlasor> hepsi, int klasorId)
        {
            var map = hepsi.ToDictionary(k => k.Id);
            var yol = new List<BirimFiyatKlasor>();

            var gecerli = klasorId;
            var adim = 0;

            while (map.TryGetValue(gecerli, out var k) && adim++ < 20)
            {
                yol.Insert(0, k);
                if (!k.UstKlasorId.HasValue) break;
                gecerli = k.UstKlasorId.Value;
            }

            return yol;
        }

        private string GeciciDosyaYolu(int karsilastirmaId, KarsilastirmaTarafi taraf)
            => Path.Combine(_env.ContentRootPath, "App_Data", GeciciKlasor,
                            $"{karsilastirmaId}_{(int)taraf}.xlsx");

        private void GeciciDosyalariTemizle(int karsilastirmaId)
        {
            TryDelete(GeciciDosyaYolu(karsilastirmaId, KarsilastirmaTarafi.Tedarikci));
            TryDelete(GeciciDosyaYolu(karsilastirmaId, KarsilastirmaTarafi.Biz));
        }

        private void TryDelete(string yol)
        {
            try
            {
                if (System.IO.File.Exists(yol)) System.IO.File.Delete(yol);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Geçici dosya silinemedi: {Yol}", yol);
            }
        }

        public static string KuralAdi(EslestirmeKurali k) => k switch
        {
            EslestirmeKurali.ReferansVeAy => "Referans no + aynı ay",
            EslestirmeKurali.SadeceReferans => "Sadece referans no",
            EslestirmeKurali.ReferansVeTarih => "Referans no + birebir tarih",
            EslestirmeKurali.ReferansVeEnYakinOnceki => "Referans no + en yakın önceki tarih",
            _ => k.ToString()
        };

        public static string KurKaynakAdi(KurKaynagi k, decimal? sabit) => k switch
        {
            KurKaynagi.TcmbFaturaTarihi => "TCMB — fatura tarihindeki kur",
            KurKaynagi.SabitKur => $"Sabit kur ({sabit?.ToString("N4", CultureInfo.GetCultureInfo("tr-TR")) ?? "-"})",
            KurKaynagi.ExcelKolonu => "Excel'deki kur kolonu",
            _ => k.ToString()
        };

        private static string DurumAdi(SonucDurumu d, BirimFiyatKarsilastirma k) => d switch
        {
            SonucDurumu.Eslesti => "Eşleşti",
            SonucDurumu.SadeceTedarikci => $"Sadece {k.TedarikciEtiket}",
            SonucDurumu.SadeceBiz => $"Sadece {k.BizEtiket}",
            _ => d.ToString()
        };

        private static string Temizle(string s)
        {
            var gecersiz = Path.GetInvalidFileNameChars();
            var temiz = new string(s.Where(c => !gecersiz.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(temiz) ? "karsilastirma" : temiz.Trim();
        }

        private static string EnIcMesaj(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }
    }
}