using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Lojistik.Controllers
{
    // ═══════════════════════════════════════════════════════════════════
    //  STOK ÖNERİ — hangi hastaneye, hangi üründen, kaç kutu gönderilmeli?
    // ═══════════════════════════════════════════════════════════════════
    //  Excel çıktıları bilerek SADE tutuldu: her dosyada yalnızca karar
    //  vermek için gereken sütunlar var (ürün, stok, aylık tüketim, kaç ay
    //  yeter, önerilen kutu). Hesabın ayrıntısı ekranda ve Raporlama'da.
    // ═══════════════════════════════════════════════════════════════════
    [Area("Lojistik")]
    [Authorize(Roles = "Yönetici,KotaTakip,Lojistik")]
    public class StokOneriController : Controller
    {
        private readonly IStokHesaplamaService _hesap;

        private const string ExcelTipi = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public StokOneriController(IStokHesaplamaService hesap)
        {
            _hesap = hesap;
        }

        private int HedefAy(int? istenen)
        {
            var ay = istenen ?? _hesap.Ayar.HedefAy;
            return ay < 1 || ay > 12 ? _hesap.Ayar.HedefAy : ay;
        }

        // ═══════════════════════════════════════════════════════════
        //  TÜM HASTANELER — nerede sorun var?
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(int? hedefAy, CancellationToken ct)
        {
            var vm = await _hesap.OzetAsync(HedefAy(hedefAy), ct);
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  HASTANE DETAY — ürün ürün stok ve öneri
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> HastaneDetay(int id, int? runwayAy, CancellationToken ct = default)
        {
            var panel = await _hesap.HesaplaHastaneAsync(id, HedefAy(runwayAy), ct);
            if (panel == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            return View(panel);
        }

        // ═══════════════════════════════════════════════════════════
        //  EXCEL — tek hastane
        //    tumu = false → yalnızca sipariş edilecek ürünler
        //    tumu = true  → hastanedeki tüm ürünlerin stok durumu
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Excel(int id, int? runwayAy, bool tumu = false, CancellationToken ct = default)
        {
            var panel = await _hesap.HesaplaHastaneAsync(id, HedefAy(runwayAy), ct);
            if (panel == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var satirlar = tumu
                ? panel.Satirlar.Where(s => s.Durum != StokDurumu.VeriYok).ToList()
                : panel.Satirlar.Where(s => s.OnerilenTest > 0).ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(tumu ? "Stok Durumu" : "Sipariş Önerisi");

            Baslik(ws, panel.HospitalName,
                $"{(tumu ? "Stok durumu" : "Sipariş önerisi")} · {DateTime.Now:dd.MM.yyyy} · " +
                (panel.SonSayimTarihi.HasValue ? $"son sayım {panel.SonSayimTarihi:dd.MM.yyyy}" : "fiziki sayım yok") +
                $" · hedef {panel.HedefRunwayAy} aylık stok");

            UrunTablosu(ws, 4, satirlar.Select(s => ((string?)null, s)).ToList());

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var ad = $"{(tumu ? "stok-durumu" : "siparis-onerisi")}-{DosyaAdi(panel.HospitalName)}-{DateTime.Now:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), ExcelTipi, ad);
        }

        // ═══════════════════════════════════════════════════════════
        //  EXCEL — YÖNETİM ÖZETİ
        //    1. sayfa "Özet"              : hastane başına bir satır
        //    2. sayfa "Sipariş Edilecekler": gönderilmesi önerilen ürünler
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> OzetExcel(int? hedefAy, CancellationToken ct)
        {
            var hedef = HedefAy(hedefAy);
            var ozet = await _hesap.OzetAsync(hedef, ct);

            var paneller = await _hesap.HesaplaTopluAsync(
                ozet.Hastaneler.Select(h => h.HospitalID).ToList(), hedef, null, ct);

            using var wb = new XLWorkbook();

            // ── 1) Özet ──
            var o = wb.Worksheets.Add("Özet");
            Baslik(o, "Lojistik Yönetim Özeti",
                $"{DateTime.Now:dd.MM.yyyy HH:mm} · hedef {hedef} aylık stok · tedarik süresi {ozet.TedarikGun} gün");

            var genel = new (string Etiket, int Deger)[]
            {
                ("Takip edilen hastane", ozet.HastaneSayisi),
                ("Stoğu kritik ürün (1 aydan az yetecek)", ozet.ToplamKritik),
                ("Stoğu düşük ürün (2 aydan az yetecek)", ozet.ToplamDusuk),
                ("Sipariş edilmesi önerilen ürün", ozet.ToplamOnerilenKalem),
                ("Sipariş edilmesi önerilen toplam kutu", ozet.ToplamOnerilenKutu)
            };

            var r = 4;
            foreach (var (etiket, deger) in genel)
            {
                o.Cell(r, 1).Value = etiket;
                o.Cell(r, 2).Value = deger;
                o.Cell(r, 2).Style.Font.Bold = true;
                r++;
            }

            r++;
            var basSatir = r;
            var basliklar = new[]
            {
                "Hastane", "Son Sayım", "Ürün Sayısı", "Kritik Ürün", "Düşük Ürün",
                "Sipariş Edilecek Ürün", "Sipariş Edilecek Kutu", "Bekleyen Sipariş"
            };
            for (var i = 0; i < basliklar.Length; i++) o.Cell(r, i + 1).Value = basliklar[i];
            BaslikBicimle(o.Range(r, 1, r, basliklar.Length));
            r++;

            foreach (var h in ozet.Hastaneler)
            {
                o.Cell(r, 1).Value = h.HospitalName;

                if (h.SonSayimTarihi.HasValue)
                {
                    o.Cell(r, 2).Value = h.SonSayimTarihi.Value;
                    o.Cell(r, 2).Style.DateFormat.Format = "dd.MM.yyyy";
                }
                else
                {
                    o.Cell(r, 2).Value = "sayım yok";
                    o.Cell(r, 2).Style.Font.FontColor = XLColor.Gray;
                }

                o.Cell(r, 3).Value = h.UrunSayisi;
                o.Cell(r, 4).Value = h.KritikSayisi;
                o.Cell(r, 5).Value = h.DusukSayisi;
                o.Cell(r, 6).Value = h.OnerilenKalem;
                o.Cell(r, 7).Value = h.OnerilenKutu;
                o.Cell(r, 8).Value = h.BekleyenTalepSayisi;

                if (h.KritikSayisi > 0) o.Cell(r, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#fee2e2");
                if (h.DusukSayisi > 0) o.Cell(r, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#ffedd5");
                r++;
            }

            o.Column(1).Width = 46;
            o.Columns(2, basliklar.Length).Width = 18;
            o.Range(basSatir, 2, Math.Max(basSatir, r - 1), basliklar.Length)
                .Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.SheetView.FreezeRows(basSatir);
            if (ozet.Hastaneler.Count > 0)
                o.Range(basSatir, 1, r - 1, basliklar.Length).SetAutoFilter();

            // ── 2) Sipariş edilecekler ──
            var s = wb.Worksheets.Add("Sipariş Edilecekler");
            Baslik(s, "Sipariş Edilmesi Önerilen Ürünler",
                "Stoğu hedefin altına inen ürünler · en acil olan en üstte");

            var edilecekler = ozet.Hastaneler
                .Where(h => paneller.ContainsKey(h.HospitalID))
                .SelectMany(h => paneller[h.HospitalID].Satirlar
                    .Where(x => x.OnerilenTest > 0)
                    .Select(x => ((string?)h.HospitalName, x)))
                .OrderBy(x => x.Item2.StokAy ?? 99m)
                .ThenBy(x => x.Item1)
                .ToList();

            UrunTablosu(s, 4, edilecekler);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return File(ms.ToArray(), ExcelTipi, $"lojistik-yonetim-ozeti-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ═══════════════════════════════════════════════════════════
        //  Excel yardımcıları
        // ═══════════════════════════════════════════════════════════

        private static void Baslik(IXLWorksheet ws, string baslik, string altBaslik)
        {
            ws.Cell(1, 1).Value = baslik;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            ws.Cell(2, 1).Value = altBaslik;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        }

        /// <summary>
        /// Ürün tablosu — 7 sütun (hastane sütunu varsa 8). Miktarlar kutu
        /// cinsindendir; kutuyla satılmayan üründe adet yazılır ve "Birim"
        /// sütununda belirtilir.
        /// </summary>
        private static void UrunTablosu(IXLWorksheet ws, int baslikSatiri, List<(string? Hastane, StokHesapSatirVm Satir)> satirlar)
        {
            var hastaneVar = satirlar.Any(x => x.Hastane != null);

            var basliklar = new List<string>();
            if (hastaneVar) basliklar.Add("Hastane");
            basliklar.AddRange(new[]
            {
                "Ürün", "Durum", "Stok", "Aylık Tüketim", "Kaç Ay Yeter", "Önerilen Sipariş", "Birim"
            });

            for (var i = 0; i < basliklar.Count; i++) ws.Cell(baslikSatiri, i + 1).Value = basliklar[i];
            BaslikBicimle(ws.Range(baslikSatiri, 1, baslikSatiri, basliklar.Count));

            var r = baslikSatiri + 1;
            foreach (var (hastane, s) in satirlar)
            {
                var c = 1;
                if (hastaneVar) ws.Cell(r, c++).Value = hastane;

                ws.Cell(r, c++).Value = string.IsNullOrWhiteSpace(s.ReferenceNumber)
                    ? s.TestAdi
                    : $"{s.TestAdi} ({s.ReferenceNumber})";

                var durum = ws.Cell(r, c++);
                durum.Value = DurumAdi(s.Durum);
                durum.Style.Fill.BackgroundColor = XLColor.FromHtml(s.Durum switch
                {
                    StokDurumu.Kritik => "#fee2e2",
                    StokDurumu.Dusuk => "#ffedd5",
                    StokDurumu.Izlemede => "#fef9c3",
                    StokDurumu.Saglikli => "#dcfce7",
                    _ => "#f1f5f9"
                });

                // Stok ve tüketim: kutu birimli üründe kutu, değilse adet
                ws.Cell(r, c++).Value = s.StokKutu ?? s.HesapGuncelStok;
                ws.Cell(r, c++).Value = s.AylikKutu ?? s.OrtalamaAylikKullanim;

                if (s.StokAy.HasValue) ws.Cell(r, c).Value = s.StokAy.Value;
                else ws.Cell(r, c).Value = "—";
                c++;

                var oneri = ws.Cell(r, c++);
                oneri.Value = s.OnerilenKutu;
                if (s.OnerilenKutu > 0) oneri.Style.Font.Bold = true;

                ws.Cell(r, c++).Value = s.Birim;
                r++;
            }

            if (satirlar.Count == 0)
            {
                ws.Cell(r, 1).Value = "Sipariş edilmesi gereken ürün yok.";
                ws.Cell(r, 1).Style.Font.FontColor = XLColor.Gray;
            }

            var ilkSayi = hastaneVar ? 3 : 2;
            if (hastaneVar) ws.Column(1).Width = 40;
            ws.Column(hastaneVar ? 2 : 1).Width = 48;
            ws.Columns(ilkSayi, basliklar.Count).Width = 17;
            ws.Range(baslikSatiri, ilkSayi, Math.Max(baslikSatiri, r - 1), basliklar.Count)
                .Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.SheetView.FreezeRows(baslikSatiri);

            if (satirlar.Count > 0)
                ws.Range(baslikSatiri, 1, r - 1, basliklar.Count).SetAutoFilter();
        }

        private static void BaslikBicimle(IXLRange aralik)
        {
            aralik.Style.Font.Bold = true;
            aralik.Style.Font.FontColor = XLColor.White;
            aralik.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            aralik.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            aralik.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static string DosyaAdi(string ad)
        {
            var temiz = new string((ad ?? "hastane")
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray());
            while (temiz.Contains("--")) temiz = temiz.Replace("--", "-");
            temiz = temiz.Trim('-');
            return temiz.Length > 40 ? temiz.Substring(0, 40) : temiz;
        }

        private static string DurumAdi(StokDurumu d) => d switch
        {
            StokDurumu.Kritik => "Kritik",
            StokDurumu.Dusuk => "Düşük",
            StokDurumu.Izlemede => "İzlemede",
            StokDurumu.Saglikli => "Yeterli",
            StokDurumu.Durgun => "Durgun",
            _ => "Veri yok"
        };
    }
}
