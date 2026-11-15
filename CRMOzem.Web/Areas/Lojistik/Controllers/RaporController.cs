using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Lojistik.Controllers
{
    // ═══════════════════════════════════════════════════════════════════
    //  RAPORLAMA — stok, gönderim ve kullanım yan yana
    // ═══════════════════════════════════════════════════════════════════
    //   Index    : hastane hastane — elde ne var, ne gönderdik, ne kullanıldı
    //   Hastane  : tek hastane — ay ay gönderim/kullanım + ürün listesi
    //   Urun     : tek ürün — hangi hastanede ne kadar var ("nerede ne var")
    //
    //  Stok rakamı Stok Öneri ve Doğrulama ile AYNI hesaptan gelir
    //  (IStokHesaplamaService). Gönderim → MonthlyShipments, kullanım →
    //  MonthlyStatistics (İstatistik modülündeki aylık girişler).
    //
    //  Yalnızca okur; hiçbir kaydı değiştirmez.
    // ═══════════════════════════════════════════════════════════════════
    [Area("Lojistik")]
    [Authorize(Roles = "Yönetici,KotaTakip,Lojistik")]
    public class RaporController : Controller
    {
        private readonly IStokHesaplamaService _hesap;
        private readonly ApplicationDbContext _db;

        private const string ExcelTipi = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public RaporController(IStokHesaplamaService hesap, ApplicationDbContext db)
        {
            _hesap = hesap;
            _db = db;
        }

        private static int Donem(int? ay) => ay is 3 or 6 or 12 ? ay.Value : 6;

        // ═══════════════════════════════════════════════════════════
        //  TÜM HASTANELER
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(int? ay, CancellationToken ct)
        {
            var vm = await IndexVmAsync(Donem(ay), ct);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Excel(int? ay, CancellationToken ct)
        {
            var vm = await IndexVmAsync(Donem(ay), ct);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Hastaneler");

            ws.Cell(1, 1).Value = "Stok – Gönderim – Kullanım Raporu";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value =
                $"{DateTime.Now:dd.MM.yyyy} · dönem: {vm.DonemBas:MM.yyyy} – {vm.DonemSon:MM.yyyy} (son {vm.DonemAy} ay) · miktarlar kutu";
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

            var basliklar = new[]
            {
                "Hastane", "Son Sayım", "Eldeki Stok", "Aylık Tüketim", "Kaç Ay Yeter",
                "Dönemde Gönderilen", "Dönemde Kullanılan", "Fark", "Durum"
            };
            const int bas = 4;
            for (var i = 0; i < basliklar.Length; i++) ws.Cell(bas, i + 1).Value = basliklar[i];

            var baslik = ws.Range(bas, 1, bas, basliklar.Length);
            baslik.Style.Font.Bold = true;
            baslik.Style.Font.FontColor = XLColor.White;
            baslik.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            baslik.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var r = bas + 1;
            foreach (var h in vm.Hastaneler)
            {
                ws.Cell(r, 1).Value = h.HospitalName;

                if (h.SonSayimTarihi.HasValue)
                {
                    ws.Cell(r, 2).Value = h.SonSayimTarihi.Value;
                    ws.Cell(r, 2).Style.DateFormat.Format = "dd.MM.yyyy";
                }
                else ws.Cell(r, 2).Value = "sayım yok";

                ws.Cell(r, 3).Value = Math.Round(h.StokKutu, 0);
                ws.Cell(r, 4).Value = Math.Round(h.AylikKutu, 1);
                if (h.StokAy.HasValue) ws.Cell(r, 5).Value = h.StokAy.Value; else ws.Cell(r, 5).Value = "—";
                ws.Cell(r, 6).Value = Math.Round(h.GonderilenKutu, 0);
                ws.Cell(r, 7).Value = Math.Round(h.KullanilanKutu, 0);
                ws.Cell(r, 8).Value = Math.Round(h.FarkKutu, 0);

                var d = ws.Cell(r, 9);
                d.Value = DengeAdi(h.Denge);
                d.Style.Fill.BackgroundColor = XLColor.FromHtml(h.Denge switch
                {
                    DengeDurumu.Dengeli => "#dcfce7",
                    DengeDurumu.Birikiyor => "#fef9c3",
                    DengeDurumu.Eriyor => "#ffedd5",
                    _ => "#f1f5f9"
                });
                r++;
            }

            // Toplam satırı
            ws.Cell(r, 1).Value = "TOPLAM";
            ws.Cell(r, 3).Value = Math.Round(vm.ToplamStok, 0);
            ws.Cell(r, 4).Value = Math.Round(vm.ToplamAylik, 1);
            if (vm.ToplamStokAy.HasValue) ws.Cell(r, 5).Value = vm.ToplamStokAy.Value;
            ws.Cell(r, 6).Value = Math.Round(vm.ToplamGonderilen, 0);
            ws.Cell(r, 7).Value = Math.Round(vm.ToplamKullanilan, 0);
            ws.Cell(r, 8).Value = Math.Round(vm.ToplamGonderilen - vm.ToplamKullanilan, 0);
            ws.Range(r, 1, r, basliklar.Length).Style.Font.Bold = true;
            ws.Range(r, 1, r, basliklar.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

            ws.Column(1).Width = 46;
            ws.Columns(2, basliklar.Length).Width = 19;
            ws.Range(bas, 2, r, basliklar.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.SheetView.FreezeRows(bas);
            if (vm.Hastaneler.Count > 0) ws.Range(bas, 1, r - 1, basliklar.Length).SetAutoFilter();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), ExcelTipi, $"stok-gonderim-kullanim-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK HASTANE
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Hastane(int id, int? ay, CancellationToken ct)
        {
            var donem = Donem(ay);
            var (bas, son) = DonemAraligi(donem);

            var panel = await _hesap.HesaplaHastaneAsync(id, _hesap.Ayar.HedefAy, ct);
            if (panel == null)
            {
                TempData["Error"] = "Hastane bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var hareket = await HareketYukleAsync(new[] { id }, bas, son, ct);
            var kutuIci = await KutuIciYukleAsync(ct);

            // Hastanenin kendi sayımındaki kutu içi adet katalogdakinden önceliklidir
            var hastaneKutuIci = panel.Satirlar
                .Where(s => s.TestID.HasValue)
                .GroupBy(s => s.TestID!.Value)
                .ToDictionary(g => g.Key, g => g.First().KutuBirimli ? Math.Max(1, g.First().BoxTextCount) : 1);

            decimal Kutu(int testId, int adet) =>
                adet / (decimal)(hastaneKutuIci.TryGetValue(testId, out var k) ? k
                               : kutuIci.TryGetValue(testId, out var kk) ? kk : 1);

            var vm = new RaporHastaneVm { DonemAy = donem };

            // ── Ürünler ──
            var gonderimByTest = hareket.Gonderim.GroupBy(x => x.TestID).ToDictionary(g => g.Key, g => g.Sum(x => x.Adet));
            var kullanimByTest = hareket.Kullanim.GroupBy(x => x.TestID).ToDictionary(g => g.Key, g => g.Sum(x => x.Adet));

            foreach (var s in panel.Satirlar.Where(x => x.TestID.HasValue && x.Durum != StokDurumu.VeriYok))
            {
                var tid = s.TestID!.Value;
                vm.Urunler.Add(new RaporUrunSatirVm
                {
                    TestID = tid,
                    UrunAdi = s.TestAdi,
                    ReferenceNumber = s.ReferenceNumber,
                    Birim = s.Birim,
                    StokKutu = s.StokKutu ?? s.HesapGuncelStok,
                    AylikKutu = s.AylikKutu ?? s.OrtalamaAylikKullanim,
                    GonderilenKutu = Math.Round(Kutu(tid, gonderimByTest.GetValueOrDefault(tid)), 1),
                    KullanilanKutu = Math.Round(Kutu(tid, kullanimByTest.GetValueOrDefault(tid)), 1),
                    StokAy = s.StokAy,
                    Durum = s.Durum,
                    KullanimTakipli = s.TuketimKaynagi == TuketimKaynagi.Kullanim
                });
            }

            vm.Urunler = vm.Urunler
                .OrderBy(u => u.StokAy ?? 999m)
                .ThenBy(u => u.UrunAdi)
                .ToList();

            // ── Ay ay ──
            // Grafik de üstteki toplamlarla aynı kümeyi kullanır: kullanımı girilen ürünler
            var kullanimAylari = hareket.Kullanim.Select(x => x.Ay).ToHashSet();
            var takipliUrunler = hareket.Kullanim.Select(x => x.TestID).ToHashSet();
            for (var a = bas; a < son; a = a.AddMonths(1))
            {
                vm.Aylar.Add(new RaporAyVm
                {
                    Ay = a,
                    GonderilenKutu = Math.Round(hareket.Gonderim.Where(x => x.Ay == a && takipliUrunler.Contains(x.TestID)).Sum(x => Kutu(x.TestID, x.Adet)), 0),
                    KullanilanKutu = Math.Round(hareket.Kullanim.Where(x => x.Ay == a).Sum(x => Kutu(x.TestID, x.Adet)), 0),
                    KullanimGirildi = kullanimAylari.Contains(a)
                });
            }

            vm.Ozet = HastaneOzeti(panel, hareket, kutuIci);
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK ÜRÜN — nerede ne var?
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Urun(int? testId, CancellationToken ct)
        {
            var paneller = await TumPanellerAsync(ct);

            var vm = new RaporUrunVm { TestID = testId };

            // Seçenekler: herhangi bir hastanede hesabı çıkan ürünler
            vm.Urunler = paneller.Values
                .SelectMany(p => p.Satirlar)
                .Where(s => s.TestID.HasValue && s.Durum != StokDurumu.VeriYok)
                .GroupBy(s => s.TestID!.Value)
                .Select(g => new RaporUrunSecenekVm
                {
                    TestID = g.Key,
                    Ad = string.IsNullOrWhiteSpace(g.First().ReferenceNumber)
                        ? g.First().TestAdi
                        : $"{g.First().TestAdi} ({g.First().ReferenceNumber})"
                })
                .OrderBy(x => x.Ad)
                .ToList();

            if (testId.HasValue)
            {
                foreach (var p in paneller.Values)
                {
                    var s = p.Satirlar.FirstOrDefault(x => x.TestID == testId && x.Durum != StokDurumu.VeriYok);
                    if (s == null) continue;

                    vm.UrunAdi ??= s.TestAdi;
                    vm.ReferenceNumber ??= s.ReferenceNumber;
                    vm.Birim = s.Birim;

                    vm.Hastaneler.Add(new RaporUrunHastaneSatirVm
                    {
                        HospitalID = p.HospitalID,
                        HospitalName = p.HospitalName,
                        StokKutu = s.StokKutu ?? s.HesapGuncelStok,
                        AylikKutu = s.AylikKutu ?? s.OrtalamaAylikKullanim,
                        StokAy = s.StokAy,
                        Durum = s.Durum,
                        SonSayimTarihi = s.SonSayimTarihi
                    });
                }

                vm.Hastaneler = vm.Hastaneler.OrderByDescending(h => h.StokKutu).ToList();
            }

            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  Ortak hesap
        // ═══════════════════════════════════════════════════════════

        private async Task<RaporIndexVm> IndexVmAsync(int donem, CancellationToken ct)
        {
            var (bas, son) = DonemAraligi(donem);
            var paneller = await TumPanellerAsync(ct);

            var hareket = await HareketYukleAsync(paneller.Keys.ToList(), bas, son, ct);
            var kutuIci = await KutuIciYukleAsync(ct);

            var vm = new RaporIndexVm { DonemAy = donem, DonemBas = bas, DonemSon = son.AddMonths(-1) };

            foreach (var p in paneller.Values)
            {
                var hHareket = new Hareketler
                {
                    Gonderim = hareket.Gonderim.Where(x => x.HospitalID == p.HospitalID).ToList(),
                    Kullanim = hareket.Kullanim.Where(x => x.HospitalID == p.HospitalID).ToList()
                };

                var satir = HastaneOzeti(p, hHareket, kutuIci);

                // Ne stoğu ne hareketi olan hastane listeyi kalabalıklaştırmasın
                if (satir.UrunSayisi == 0 && satir.GonderilenKutu == 0 && satir.KullanilanKutu == 0) continue;

                vm.Hastaneler.Add(satir);
            }

            vm.Hastaneler = vm.Hastaneler.OrderBy(h => h.HospitalName).ToList();
            return vm;
        }

        /// <summary>
        /// Hastane özeti. Gönderim–kullanım karşılaştırması yalnızca KULLANIMI
        /// TAKİP EDİLEN ürünler (reaktifler) üzerinden yapılır; kalibratör,
        /// kontrol ve sarfın kullanım kaydı olmadığından onları katmak
        /// "gönderdik ama kullanılmadı" gibi yanlış bir fark doğururdu.
        /// </summary>
        private static RaporHastaneSatirVm HastaneOzeti(
            HastaneStokPaneliVm p, Hareketler hareket, Dictionary<int, int> kutuIci)
        {
            var hesaplanan = p.Satirlar.Where(s => s.TestID.HasValue && s.Durum != StokDurumu.VeriYok).ToList();

            var hastaneKutuIci = hesaplanan
                .GroupBy(s => s.TestID!.Value)
                .ToDictionary(g => g.Key, g => g.First().KutuBirimli ? Math.Max(1, g.First().BoxTextCount) : 1);

            decimal Kutu(int testId, int adet) =>
                adet / (decimal)(hastaneKutuIci.TryGetValue(testId, out var k) ? k
                               : kutuIci.TryGetValue(testId, out var kk) ? kk : 1);

            var takipli = hareket.Kullanim.Select(x => x.TestID).ToHashSet();

            return new RaporHastaneSatirVm
            {
                HospitalID = p.HospitalID,
                HospitalName = p.HospitalName,
                SonSayimTarihi = p.SonSayimTarihi,
                KullanimSonAy = p.KullanimSonAy,
                UrunSayisi = hesaplanan.Count,
                KritikUrun = p.KritikSayisi,
                StokKutu = Math.Round(hesaplanan.Sum(s => s.StokKutu ?? s.HesapGuncelStok), 0),
                AylikKutu = Math.Round(hesaplanan.Sum(s => s.AylikKutu ?? s.OrtalamaAylikKullanim), 1),
                GonderilenKutu = Math.Round(hareket.Gonderim.Where(x => takipli.Contains(x.TestID)).Sum(x => Kutu(x.TestID, x.Adet)), 0),
                KullanilanKutu = Math.Round(hareket.Kullanim.Sum(x => Kutu(x.TestID, x.Adet)), 0)
            };
        }

        private async Task<Dictionary<int, HastaneStokPaneliVm>> TumPanellerAsync(CancellationToken ct)
        {
            var ids = await _db.Hospitals.AsNoTracking()
                .Where(h => h.Aktif)
                .Select(h => h.HospitalID)
                .ToListAsync(ct);

            return await _hesap.HesaplaTopluAsync(ids, _hesap.Ayar.HedefAy, null, ct);
        }

        /// <summary>Son N TAM ay: [bas, son) — içinde bulunulan ay dahil değil.</summary>
        private static (DateTime Bas, DateTime Son) DonemAraligi(int donem)
        {
            var buAy = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return (buAy.AddMonths(-donem), buAy);
        }

        private async Task<Hareketler> HareketYukleAsync(
            IReadOnlyCollection<int> hospitalIds, DateTime bas, DateTime son, CancellationToken ct)
        {
            var gonderim = await _db.MonthlyShipments.AsNoTracking()
                .Where(m => hospitalIds.Contains(m.HospitalID) && m.Month >= bas && m.Month < son)
                .Select(m => new Hareket { HospitalID = m.HospitalID, TestID = m.TestID, Ay = m.Month, Adet = m.SentCount })
                .ToListAsync(ct);

            var kullanim = await _db.MonthlyStatistics.AsNoTracking()
                .Where(m => hospitalIds.Contains(m.HospitalID) && m.Month >= bas && m.Month < son)
                .Select(m => new Hareket { HospitalID = m.HospitalID, TestID = m.TestID, Ay = m.Month, Adet = m.TestCount })
                .ToListAsync(ct);

            foreach (var h in gonderim) h.Ay = new DateTime(h.Ay.Year, h.Ay.Month, 1);
            foreach (var h in kullanim) h.Ay = new DateTime(h.Ay.Year, h.Ay.Month, 1);

            return new Hareketler { Gonderim = gonderim, Kullanim = kullanim };
        }

        /// <summary>Katalogdaki "kutu başına adet" — kutuyla girilmeyen üründe 1.</summary>
        private async Task<Dictionary<int, int>> KutuIciYukleAsync(CancellationToken ct)
        {
            var ham = await _db.Tests.AsNoTracking()
                .Select(t => new { t.TestID, t.GirisTipi, t.KutuBasinaAdet })
                .ToListAsync(ct);

            return ham.ToDictionary(
                t => t.TestID,
                t => t.GirisTipi == TestGirisTipi.Kutu && t.KutuBasinaAdet is > 0 ? t.KutuBasinaAdet.Value : 1);
        }

        public static string DengeAdi(DengeDurumu d) => d switch
        {
            DengeDurumu.Dengeli => "Dengeli",
            DengeDurumu.Birikiyor => "Stok birikiyor",
            DengeDurumu.Eriyor => "Stok eriyor",
            _ => "Kullanım girilmemiş"
        };

        private class Hareket
        {
            public int HospitalID { get; set; }
            public int TestID { get; set; }
            public DateTime Ay { get; set; }
            public int Adet { get; set; }
        }

        private class Hareketler
        {
            public List<Hareket> Gonderim { get; set; } = new();
            public List<Hareket> Kullanim { get; set; } = new();
        }
    }
}
