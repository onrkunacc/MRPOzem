using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    // ═══════════════════════════════════════════════════════════════════
    //  STOK HESAP MOTORU
    // ═══════════════════════════════════════════════════════════════════
    //
    //  Bir (hastane × ürün) için dört soruya cevap verir:
    //
    //   1. BUGÜN ELDE NE VAR?
    //        Sayım varsa : sayım + sonrasındaki gönderim − kullanım − tahmini tüketim
    //        Sayım yoksa : bugüne kadarki gönderim − kullanım − tahmini tüketim
    //
    //      · Sayım ayının kendi hareketleri, sayımdan sonra kalan gün oranında
    //        sayılır (sayım 10 Eylül'deyse Eylül hareketlerinin 20/30'u).
    //      · "Tahmini tüketim": hastanenin kullanımını henüz bildirmediği günler.
    //        Ekim raporu gelmediyse Ekim boyunca tüketim sıfır DEĞİLDİR —
    //        ortalamadan tahmin edilir.
    //
    //   2. AYDA NE KADAR TÜKETİYOR?
    //        Hastanenin kullanım bildirdiği son 3 ayın ortalaması.
    //        (Takvimdeki son 3 ay değil — rapor gelmeyen ay ortalamayı
    //        düşürmesin diye.) Kullanım verisi hiç olmayan üründe son 6 ayın
    //        gönderim ortalaması kullanılır.
    //
    //   3. NE KADAR SİPARİŞ GEREK?
    //        Hedef = aylık tüketim × hedef ay (varsayılan 3).
    //        Öneri = hedef − (eldeki + yoldaki), kutuya yukarı yuvarlanır.
    //        Stok hedefin yarım ay altına inmeden öneri çıkmaz.
    //
    //   4. BU RAKAMA NE KADAR GÜVENEBİLİRİM?
    //        Sayımın yaşı, raporlanan ay sayısı ve tahmin edilen gün sayısına
    //        göre Yüksek / Orta / Düşük.
    //
    //  Tüm miktarlar TEST (adet) cinsindendir; kutu yalnızca gösterim ve
    //  yuvarlama içindir.
    // ═══════════════════════════════════════════════════════════════════
    public class StokHesaplamaService : IStokHesaplamaService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StokHesaplamaService> _log;

        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        public LojistikAyar Ayar { get; }

        /// <summary>Yalnızca test için: "bugün"ü sabitler.</summary>
        public DateTime? BugunSabit { get; set; }
        private DateTime Bugun => (BugunSabit ?? DateTime.Today).Date;

        public StokHesaplamaService(
            ApplicationDbContext db,
            ILogger<StokHesaplamaService> log,
            IConfiguration config)
        {
            _db = db;
            _log = log;
            Ayar = AyarOku(config);
        }

        private static LojistikAyar AyarOku(IConfiguration? config)
        {
            var a = new LojistikAyar();
            if (config == null) return a;

            if (int.TryParse(config["Lojistik:HedefAy"], out var hedef) && hedef > 0 && hedef <= 12)
                a.HedefAy = hedef;

            if (int.TryParse(config["Lojistik:TedarikGun"], out var tedarik) && tedarik >= 0 && tedarik <= 180)
                a.TedarikGun = tedarik;

            if (int.TryParse(config["Lojistik:OrtalamaAy"], out var ort) && ort >= 1 && ort <= 12)
                a.OrtalamaAy = ort;

            return a;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK HASTANE / TEK ÜRÜN
        // ═══════════════════════════════════════════════════════════

        public async Task<HastaneStokPaneliVm?> HesaplaHastaneAsync(
            int hospitalId, int hedefRunwayAy = 3, CancellationToken ct = default)
        {
            var hepsi = await HesaplaTopluAsync(new[] { hospitalId }, hedefRunwayAy, null, ct);
            return hepsi.TryGetValue(hospitalId, out var panel) ? panel : null;
        }

        public async Task<StokHesapSatirVm?> HesaplaUrunAsync(
            int hospitalId, int testId, int hedefRunwayAy = 3, CancellationToken ct = default)
        {
            var hepsi = await HesaplaTopluAsync(
                new[] { hospitalId }, hedefRunwayAy, new[] { (hospitalId, testId) }, ct);

            return hepsi.TryGetValue(hospitalId, out var panel)
                ? panel.Satirlar.FirstOrDefault(s => s.TestID == testId)
                : null;
        }

        // ═══════════════════════════════════════════════════════════
        //  TOPLU HESAP — hastane sayısından bağımsız sabit sorgu
        // ═══════════════════════════════════════════════════════════

        public async Task<Dictionary<int, HastaneStokPaneliVm>> HesaplaTopluAsync(
            IReadOnlyCollection<int> hospitalIds,
            int hedefAy = 3,
            IEnumerable<(int HospitalID, int TestID)>? ekUrunler = null,
            CancellationToken ct = default)
        {
            var sonuc = new Dictionary<int, HastaneStokPaneliVm>();

            var ids = (hospitalIds ?? Array.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0) return sonuc;
            if (hedefAy <= 0) hedefAy = Ayar.HedefAy;

            var bugun = Bugun;
            var buAy = AyBasi(bugun);
            var onayPencere = buAy.AddMonths(-1);

            // ── 1) Hastaneler ──
            var hastaneler = await _db.Hospitals.AsNoTracking()
                .Where(h => ids.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            if (hastaneler.Count == 0) return sonuc;

            // ── 2) Ürün kataloğu ──
            var testler = await _db.Tests.AsNoTracking()
                .Select(t => new TestBilgi
                {
                    TestID = t.TestID,
                    TestName = t.TestName,
                    SutCode = t.SutCode,
                    Kategori = t.Kategori,
                    Platform = t.Platform,
                    GirisTipi = t.GirisTipi,
                    KutuBasinaAdet = t.KutuBasinaAdet
                })
                .ToListAsync(ct);

            var testById = testler.ToDictionary(t => t.TestID);

            var testByRef = new Dictionary<string, int>();
            foreach (var t in testler)
            {
                var anahtar = ReferansKodu.Normalize(t.SutCode);
                if (anahtar.Length > 0 && !testByRef.ContainsKey(anahtar))
                    testByRef[anahtar] = t.TestID;
            }

            // Kullanımı (istatistiği) tutulan ürünler — pratikte reaktifler.
            // Bir ürünün herhangi bir hastanede kullanım kaydı varsa "takipli"dir.
            var takipliListe = await _db.MonthlyStatistics.AsNoTracking()
                .Select(s => s.TestID)
                .Distinct()
                .ToListAsync(ct);
            var takipliTestler = takipliListe.ToHashSet();

            // ── 3) Fiziki sayım ──
            var snapHam = await _db.FizikiSayimSnapshotlari.AsNoTracking()
                .Where(s => ids.Contains(s.HospitalID) && s.SayimAktifMi)
                .Select(s => new SnapBilgi
                {
                    HospitalID = s.HospitalID,
                    MappedTestID = s.MappedTestID,
                    ReferenceNumber = s.ReferenceNumber,
                    ProductName = s.ProductName,
                    Platform = s.Platform,
                    UrunTipi = s.UrunTipi,
                    SayimNo = s.SayimNo,
                    SayimTarihi = s.SayimTarihi,
                    TestCount = s.TestCount,
                    BoxTextCount = s.BoxTextCount
                })
                .ToListAsync(ct);

            // Sayım senkronunda eşleşmemiş ürünü burada bir kez daha dene:
            // referans yazımı farklıysa (tire, nokta) yeni kural tutabilir.
            foreach (var s in snapHam)
            {
                if (s.MappedTestID.HasValue && testById.ContainsKey(s.MappedTestID.Value))
                {
                    s.EtkinTestID = s.MappedTestID;
                    continue;
                }

                var anahtar = ReferansKodu.Normalize(s.ReferenceNumber);
                if (anahtar.Length > 0 && testByRef.TryGetValue(anahtar, out var tid))
                    s.EtkinTestID = tid;
            }

            var snaplar = snapHam.Where(s => s.EtkinTestID.HasValue).ToList();

            // ── 4) Hareket penceresi ──
            // Normalde son 12 ay; sayım daha eskiyse o aya kadar (en çok 36 ay).
            var pencereBas = buAy.AddMonths(-12);
            if (snaplar.Count > 0)
            {
                var enEskiSayim = AyBasi(snaplar.Min(s => s.SayimTarihi));
                if (enEskiSayim < pencereBas) pencereBas = enEskiSayim;
                var altSinir = buAy.AddMonths(-36);
                if (pencereBas < altSinir) pencereBas = altSinir;
            }

            var gonderimler = await _db.MonthlyShipments.AsNoTracking()
                .Where(m => ids.Contains(m.HospitalID) && m.Month >= pencereBas)
                .Select(m => new Hareket { HospitalID = m.HospitalID, TestID = m.TestID, Ay = m.Month, Adet = m.SentCount })
                .ToListAsync(ct);

            var kullanimlar = await _db.MonthlyStatistics.AsNoTracking()
                .Where(m => ids.Contains(m.HospitalID) && m.Month >= pencereBas)
                .Select(m => new Hareket { HospitalID = m.HospitalID, TestID = m.TestID, Ay = m.Month, Adet = m.TestCount })
                .ToListAsync(ct);

            foreach (var h in gonderimler) h.Ay = AyBasi(h.Ay);
            foreach (var h in kullanimlar) h.Ay = AyBasi(h.Ay);

            // Sayımı olmayan ürünler için "kayıt farkı": tüm zamanların toplamı
            var gonderimToplamHam = await _db.MonthlyShipments.AsNoTracking()
                .Where(m => ids.Contains(m.HospitalID))
                .GroupBy(m => new { m.HospitalID, m.TestID })
                .Select(g => new { g.Key.HospitalID, g.Key.TestID, Toplam = g.Sum(x => x.SentCount) })
                .ToListAsync(ct);

            var kullanimToplamHam = await _db.MonthlyStatistics.AsNoTracking()
                .Where(m => ids.Contains(m.HospitalID))
                .GroupBy(m => new { m.HospitalID, m.TestID })
                .Select(g => new { g.Key.HospitalID, g.Key.TestID, Toplam = g.Sum(x => x.TestCount) })
                .ToListAsync(ct);

            var gonderimToplam = gonderimToplamHam.ToDictionary(x => (x.HospitalID, x.TestID), x => x.Toplam);
            var kullanimToplam = kullanimToplamHam.ToDictionary(x => (x.HospitalID, x.TestID), x => x.Toplam);

            // ── 5) Aktif test şablonları ──
            var sablonlar = await _db.TestTemplates.AsNoTracking()
                .Where(t => ids.Contains(t.HospitalID) && t.IsActive)
                .Select(t => new { t.HospitalID, t.TestID })
                .ToListAsync(ct);

            // ── 6) Açık siparişler: bekleyen + yakın zamanda onaylanan ──
            var acikKalemler = await _db.SiparisTalepKalemleri.AsNoTracking()
                .Where(k => k.TestID != null
                         && ids.Contains(k.SiparisTalep!.HospitalID)
                         && (k.SiparisTalep.Durum == SiparisTalepDurumu.Beklemede
                             || ((k.SiparisTalep.Durum == SiparisTalepDurumu.OnaylandiAynen
                                  || k.SiparisTalep.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi)
                                 && k.SiparisTalep.OnayTarihi >= onayPencere)))
                .Select(k => new AcikKalem
                {
                    HospitalID = k.SiparisTalep!.HospitalID,
                    TestID = k.TestID!.Value,
                    Durum = k.SiparisTalep.Durum,
                    OnayTarihi = k.SiparisTalep.OnayTarihi,
                    TalepTest = k.HastaneTalepTest,
                    OnayTest = k.OnaylananTest
                })
                .ToListAsync(ct);

            // ── Bellek içi indeksler ──
            var snapByKey = snaplar.ToLookup(s => (s.HospitalID, s.EtkinTestID!.Value));
            var gonderimByKey = gonderimler.ToLookup(g => (g.HospitalID, g.TestID));
            var kullanimByKey = kullanimlar.ToLookup(k => (k.HospitalID, k.TestID));
            var kullanimByHastane = kullanimlar.ToLookup(k => k.HospitalID);
            var gonderimByHastane = gonderimler.ToLookup(g => g.HospitalID);
            var acikByKey = acikKalemler.ToLookup(a => (a.HospitalID, a.TestID));
            var ekByHastane = (ekUrunler ?? Enumerable.Empty<(int HospitalID, int TestID)>())
                .ToLookup(e => e.HospitalID, e => e.TestID);

            // ═══════════════════════════════════════════════════════
            //  HASTANE DÖNGÜSÜ
            // ═══════════════════════════════════════════════════════
            foreach (var h in hastaneler)
            {
                var hid = h.HospitalID;

                var panel = new HastaneStokPaneliVm
                {
                    HospitalID = hid,
                    HospitalName = h.HospitalName,
                    HedefRunwayAy = hedefAy,
                    TedarikGun = Ayar.TedarikGun
                };

                var hSnaplar = snaplar.Where(s => s.HospitalID == hid).ToList();
                if (hSnaplar.Count > 0)
                {
                    var enYeni = hSnaplar.OrderByDescending(s => s.SayimTarihi).First();
                    panel.SonSayimTarihi = enYeni.SayimTarihi;
                    panel.SonSayimNo = enYeni.SayimNo;
                }

                // ── Kullanım raporlamasının durumu ──
                var hKullanimAylari = kullanimByHastane[hid].Select(k => k.Ay).Distinct().ToList();
                var raporluAylar = hKullanimAylari.Where(a => a < buAy).OrderByDescending(a => a).ToList();

                DateTime? kullanimSonAy = hKullanimAylari.Count > 0 ? hKullanimAylari.Max() : null;
                panel.KullanimSonAy = kullanimSonAy;
                panel.RaporlananAySayisi = raporluAylar.Count(a => a >= buAy.AddMonths(-12));

                var hGonderimAylari = gonderimByHastane[hid].Select(g => g.Ay).Distinct().ToList();
                panel.GonderimSonAy = hGonderimAylari.Count > 0 ? hGonderimAylari.Max() : null;

                // Kullanımın bilindiği son an (bu tarihten sonrası tahmin edilir)
                DateTime? kullanimBilinenSon = null;
                if (kullanimSonAy.HasValue)
                    kullanimBilinenSon = kullanimSonAy.Value >= buAy ? bugun : kullanimSonAy.Value.AddMonths(1);

                var ortAylar = raporluAylar.Take(Ayar.OrtalamaAy).ToList();
                var oncekiAylar = raporluAylar.Skip(Ayar.OrtalamaAy).Take(Ayar.OrtalamaAy).ToList();

                // ── Ürün evreni ──
                var evren = new HashSet<int>();
                foreach (var s in hSnaplar) evren.Add(s.EtkinTestID!.Value);
                foreach (var s in sablonlar.Where(x => x.HospitalID == hid)) evren.Add(s.TestID);
                foreach (var g in gonderimByHastane[hid]) evren.Add(g.TestID);
                foreach (var k in kullanimByHastane[hid]) evren.Add(k.TestID);
                foreach (var e in ekByHastane[hid]) evren.Add(e);

                foreach (var testId in evren)
                {
                    if (!testById.TryGetValue(testId, out var test)) continue;

                    var satir = SatirHesapla(
                        hid, test, hedefAy, bugun, buAy,
                        snapByKey[(hid, testId)].ToList(),
                        AylikTopla(gonderimByKey[(hid, testId)]),
                        AylikTopla(kullanimByKey[(hid, testId)]),
                        gonderimToplam.TryGetValue((hid, testId), out var gt) ? gt : 0,
                        kullanimToplam.TryGetValue((hid, testId), out var kt) ? kt : 0,
                        takipliTestler.Contains(testId),
                        ortAylar, oncekiAylar, kullanimBilinenSon,
                        acikByKey[(hid, testId)].ToList());

                    panel.Satirlar.Add(satir);
                }

                panel.Satirlar = panel.Satirlar
                    .OrderBy(s => DurumSirasi(s.Durum))
                    .ThenByDescending(s => s.OnerilenTest)
                    .ThenBy(s => s.TestAdi)
                    .ToList();

                HastaneUyarilari(panel, bugun, buAy);
                sonuc[hid] = panel;
            }

            return sonuc;
        }

        // ═══════════════════════════════════════════════════════════
        //  TEK SATIR
        // ═══════════════════════════════════════════════════════════
        private StokHesapSatirVm SatirHesapla(
            int hospitalId,
            TestBilgi test,
            int hedefAy,
            DateTime bugun,
            DateTime buAy,
            List<SnapBilgi> snaplar,
            Dictionary<DateTime, int> gonderimAylik,
            Dictionary<DateTime, int> kullanimAylik,
            int gonderimTumZaman,
            int kullanimTumZaman,
            bool takipli,
            List<DateTime> ortAylar,
            List<DateTime> oncekiAylar,
            DateTime? kullanimBilinenSon,
            List<AcikKalem> acikKalemler)
        {
            // Yalnızca ürünün EN SON sayımı kullanılır. Eski sayımın satırları
            // hâlâ "aktif" işaretli kalmışsa toplanıp stoğu ikiye katlamasın.
            if (snaplar.Count > 1)
            {
                var sonTarih = snaplar.Max(s => s.SayimTarihi);
                var sonNo = snaplar.Where(s => s.SayimTarihi == sonTarih).Max(s => s.SayimNo);
                snaplar = snaplar.Where(s => s.SayimNo == sonNo).ToList();
            }

            var ilkSnap = snaplar.OrderByDescending(s => s.SayimTarihi).FirstOrDefault();

            var satir = new StokHesapSatirVm
            {
                TestID = test.TestID,
                TestAdi = test.TestName,
                ProductName = ilkSnap?.ProductName ?? test.TestName,
                ReferenceNumber = !string.IsNullOrWhiteSpace(ilkSnap?.ReferenceNumber) ? ilkSnap!.ReferenceNumber : test.SutCode,
                Platform = ilkSnap?.Platform ?? test.Platform,
                Kategori = test.Kategori
            };

            // ── Ürün tipi ──
            if (ilkSnap != null) satir.UrunTipi = ilkSnap.UrunTipi;
            else if (takipli) satir.UrunTipi = UrunTipi.Reaktif;
            else satir.UrunTipi = UrunTipiTespitHelper.Tespit(test.TestName, test.Kategori);

            // ── Birim: 1 kutu kaç test ──
            if (test.GirisTipi == TestGirisTipi.Kutu && test.KutuBasinaAdet.GetValueOrDefault() > 0)
            {
                satir.BoxTextCount = test.KutuBasinaAdet!.Value;
                satir.KutuBirimli = true;
            }
            else if (test.GirisTipi == TestGirisTipi.Adet)
            {
                satir.BoxTextCount = 1;
                satir.KutuBirimli = false;
            }
            else if (ilkSnap != null && ilkSnap.BoxTextCount > 0)
            {
                satir.BoxTextCount = ilkSnap.BoxTextCount;
                satir.KutuBirimli = ilkSnap.BoxTextCount > 1;
            }
            var carpan = Math.Max(1, satir.BoxTextCount);

            // ═══ 2. SORU — AYDA NE KADAR TÜKETİYOR? ═══
            decimal ort = 0m;

            if (takipli && ortAylar.Count > 0)
            {
                var toplam = ortAylar.Sum(a => kullanimAylik.TryGetValue(a, out var v) ? v : 0);
                ort = (decimal)toplam / ortAylar.Count;

                satir.TuketimKaynagi = TuketimKaynagi.Kullanim;
                satir.OrtalamaAySayisi = ortAylar.Count;
                satir.Son3AyKullanim = toplam;

                if (oncekiAylar.Count == ortAylar.Count && ortAylar.Count >= 2)
                {
                    var onceki = (decimal)oncekiAylar.Sum(a => kullanimAylik.TryGetValue(a, out var v) ? v : 0)
                                 / oncekiAylar.Count;
                    if (onceki > 0)
                        satir.TrendYuzde = Math.Round((ort - onceki) / onceki * 100m, 0);
                }
            }
            else
            {
                // Kullanım verisi yok (kalibratör, kontrol, sarf ya da hiç rapor
                // vermemiş hastane): son 6 tam ayın gönderim ortalaması.
                var bas = buAy.AddMonths(-6);
                var toplam = gonderimAylik.Where(kv => kv.Key >= bas && kv.Key < buAy).Sum(kv => kv.Value);
                if (toplam > 0)
                {
                    ort = toplam / 6m;
                    satir.TuketimKaynagi = TuketimKaynagi.Gonderim;
                    satir.OrtalamaAySayisi = 6;
                    satir.Son3AyKullanim = toplam;
                }
            }

            satir.OrtalamaAylikKullanim = Math.Round(ort, 1);
            var gunluk = ort / 30m;

            // ═══ 1. SORU — BUGÜN ELDE NE VAR? ═══
            int stok = 0;
            DateTime? sayimTarihi = null;

            if (snaplar.Count > 0)
            {
                satir.StokKaynagi = StokKaynagi.FizikiSayim;

                var d = snaplar.Max(s => s.SayimTarihi).Date;
                if (d > bugun) d = bugun;
                sayimTarihi = d;

                satir.SonSayimTarihi = d;
                satir.SonSayimNo = ilkSnap!.SayimNo;
                satir.SonSayimTestCount = snaplar.Sum(s => s.TestCount);
                satir.LotSayisi = snaplar.Count;

                // Sayım ayının hareketleri: sayımdan sonra kalan gün oranında
                var sayimAyi = AyBasi(d);
                var ayGun = DateTime.DaysInMonth(d.Year, d.Month);
                var kalanOran = (decimal)(ayGun - d.Day) / ayGun;

                int Sonrasi(Dictionary<DateTime, int> aylik)
                {
                    var tam = aylik.Where(kv => kv.Key > sayimAyi).Sum(kv => kv.Value);
                    var kismi = aylik.TryGetValue(sayimAyi, out var v) ? (int)Math.Round(v * kalanOran) : 0;
                    return tam + kismi;
                }

                satir.SayimdanSonraGonderim = Sonrasi(gonderimAylik);
                satir.SayimdanSonraKullanim = satir.TuketimKaynagi == TuketimKaynagi.Kullanim
                    ? Sonrasi(kullanimAylik)
                    : 0;

                // Kullanımın raporlanmadığı günler
                var tahminBas = d;
                if (satir.TuketimKaynagi == TuketimKaynagi.Kullanim
                    && kullanimBilinenSon.HasValue && kullanimBilinenSon.Value > d)
                {
                    tahminBas = kullanimBilinenSon.Value;
                }

                satir.TahminGun = Math.Max(0, (bugun - tahminBas).Days);
                satir.TahminiTuketim = (int)Math.Round(gunluk * satir.TahminGun);

                stok = satir.SonSayimTestCount
                     + satir.SayimdanSonraGonderim
                     - satir.SayimdanSonraKullanim
                     - satir.TahminiTuketim;
            }
            else if (satir.TuketimKaynagi == TuketimKaynagi.Kullanim
                     && (gonderimTumZaman > 0 || kullanimTumZaman > 0))
            {
                // Sayım yok: kayıt farkı. Açılış stoku bilinmediği için güven düşük.
                satir.StokKaynagi = StokKaynagi.KayitFarki;
                satir.SayimdanSonraGonderim = gonderimTumZaman;
                satir.SayimdanSonraKullanim = kullanimTumZaman;

                var tahminBas = kullanimBilinenSon ?? bugun;
                satir.TahminGun = Math.Max(0, (bugun - tahminBas).Days);
                satir.TahminiTuketim = (int)Math.Round(gunluk * satir.TahminGun);

                stok = gonderimTumZaman - kullanimTumZaman - satir.TahminiTuketim;
            }
            else
            {
                satir.StokKaynagi = StokKaynagi.Yok;
            }

            satir.HesapGuncelStok = stok;
            var stokPoz = Math.Max(0, stok);

            // ── Yolda: onaylanmış ama gönderim kaydına işlenmemiş ──
            var onayli = acikKalemler
                .Where(a => a.Durum != SiparisTalepDurumu.Beklemede
                         && a.OnayTarihi.HasValue
                         && (!sayimTarihi.HasValue || a.OnayTarihi.Value.Date > sayimTarihi.Value))
                .ToList();

            if (onayli.Count > 0)
            {
                var onayToplam = onayli.Sum(a => a.OnayTest ?? 0);
                var ilkOnayAyi = AyBasi(onayli.Min(a => a.OnayTarihi!.Value));
                var oAydanBeriGonderim = gonderimAylik.Where(kv => kv.Key >= ilkOnayAyi).Sum(kv => kv.Value);
                satir.Yolda = Math.Max(0, onayToplam - oAydanBeriGonderim);
            }

            satir.BekleyenTalep = acikKalemler
                .Where(a => a.Durum == SiparisTalepDurumu.Beklemede)
                .Sum(a => a.TalepTest);

            // ── Yetme süresi ──
            var stokVar = satir.StokKaynagi != StokKaynagi.Yok;

            if (stokVar && ort > 0)
            {
                satir.StokAy = Math.Round(stokPoz / ort, 1);
                satir.RunwayGun = (int)(stokPoz / gunluk);
            }
            else
            {
                satir.RunwayGun = stokVar && stokPoz > 0 ? 9999 : 0;
            }

            // ═══ 3. SORU — NE KADAR SİPARİŞ GEREK? ═══
            satir.HedefStok = (int)Math.Round(ort * hedefAy);

            var (oneriTest, oneriKutu) = stokVar
                ? IhtiyacHesapla(ort, hedefAy, stokPoz + satir.Yolda, carpan)
                : (0, 0);

            satir.OnerilenTest = oneriTest;
            satir.OnerilenKutu = oneriKutu;

            // ── Durum ──
            satir.Durum = DurumBelirle(satir.StokKaynagi, ort, stok, satir.RunwayGun);

            // ═══ 4. SORU — NE KADAR GÜVENİLİR? ═══
            GuvenBelirle(satir, bugun, stok);

            // ── Açıklamalar ──
            AciklamaYaz(satir, hedefAy, stok, carpan);
            return satir;
        }

        /// <summary>
        /// Hedefe tamamlamak için gereken miktar. Stok hedefin
        /// <see cref="LojistikAyar.OneriEsigiAy"/> kadar altına inmediyse 0.
        /// Dönen test miktarı kutuya yukarı yuvarlanmıştır.
        /// </summary>
        private (int Test, int Kutu) IhtiyacHesapla(decimal ort, int hedefAy, int eldeki, int carpan)
        {
            if (ort <= 0) return (0, 0);

            var esik = ort * (hedefAy - Ayar.OneriEsigiAy);
            if (eldeki >= esik) return (0, 0);

            var hedef = (int)Math.Round(ort * hedefAy);
            var ham = hedef - eldeki;
            if (ham <= 0) return (0, 0);

            carpan = Math.Max(1, carpan);
            var kutu = (int)Math.Ceiling((decimal)ham / carpan);
            return (kutu * carpan, kutu);
        }

        private StokDurumu DurumBelirle(StokKaynagi kaynak, decimal ort, int stok, int runwayGun)
        {
            if (kaynak == StokKaynagi.Yok) return StokDurumu.VeriYok;
            if (ort <= 0) return stok > 0 ? StokDurumu.Durgun : StokDurumu.VeriYok;

            if (stok <= 0) return StokDurumu.Kritik;
            if (runwayGun < Ayar.KritikGun) return StokDurumu.Kritik;
            if (runwayGun < Ayar.DusukGun) return StokDurumu.Dusuk;
            if (runwayGun < Ayar.IzlemeGun) return StokDurumu.Izlemede;
            return StokDurumu.Saglikli;
        }

        private void GuvenBelirle(StokHesapSatirVm s, DateTime bugun, int stok)
        {
            if (s.StokKaynagi == StokKaynagi.Yok || s.TuketimKaynagi == TuketimKaynagi.Yok)
            {
                s.Guven = VeriGuveni.Yok;
                s.GuvenNedeni = s.StokKaynagi == StokKaynagi.Yok
                    ? "Fiziki sayım ve kullanım kaydı yok — stok hesaplanamıyor"
                    : "Tüketim verisi yok";
                return;
            }

            var seviye = VeriGuveni.Yuksek;
            var nedenler = new List<string>();

            void Dusur(VeriGuveni yeni, string neden)
            {
                if ((int)yeni < (int)seviye) seviye = yeni;
                nedenler.Add(neden);
            }

            if (s.StokKaynagi == StokKaynagi.KayitFarki)
            {
                Dusur(VeriGuveni.Dusuk, "fiziki sayım yok");
            }
            else if (s.SonSayimTarihi.HasValue)
            {
                var yas = (bugun - s.SonSayimTarihi.Value.Date).Days;
                if (yas > Ayar.SayimCokEskiGun) Dusur(VeriGuveni.Dusuk, $"sayım {yas} gün önce");
                else if (yas > Ayar.SayimEskiGun) Dusur(VeriGuveni.Orta, $"sayım {yas} gün önce");
            }

            if (s.TuketimKaynagi == TuketimKaynagi.Gonderim)
            {
                Dusur(VeriGuveni.Orta, "kullanım verisi yok, gönderim ortalaması kullanıldı");
            }
            else
            {
                if (s.OrtalamaAySayisi <= 1) Dusur(VeriGuveni.Dusuk, "yalnızca 1 ay kullanım verisi var");
                else if (s.OrtalamaAySayisi < Ayar.OrtalamaAy) Dusur(VeriGuveni.Orta, $"{s.OrtalamaAySayisi} ay kullanım verisi var");

                if (s.TahminGun > 75) Dusur(VeriGuveni.Dusuk, $"son {s.TahminGun} günün kullanımı raporlanmamış");
                else if (s.TahminGun > 45) Dusur(VeriGuveni.Orta, $"son {s.TahminGun} günün kullanımı raporlanmamış");
            }

            if (stok < 0) Dusur(VeriGuveni.Dusuk, "hesap stok eksi çıkıyor");

            s.Guven = seviye;
            s.GuvenNedeni = nedenler.Count == 0
                ? "Güncel sayım ve yeterli kullanım verisi var"
                : char.ToUpper(nedenler[0][0], TR) + nedenler[0].Substring(1)
                  + (nedenler.Count > 1 ? " · " + string.Join(" · ", nedenler.Skip(1)) : "");
        }

        private void AciklamaYaz(StokHesapSatirVm s, int hedefAy, int stok, int carpan)
        {
            string N(decimal v) => v.ToString("N0", TR);

            // ── Adım adım döküm ──
            if (s.StokKaynagi == StokKaynagi.FizikiSayim)
            {
                s.Adimlar.Add(new HesapAdimVm("", $"Fiziki sayım ({s.SonSayimTarihi:dd.MM.yyyy})", N(s.SonSayimTestCount),
                    s.LotSayisi > 1 ? $"{s.LotSayisi} lot toplamı" : null));
                s.Adimlar.Add(new HesapAdimVm("+", "Sayımdan sonra gönderim", N(s.SayimdanSonraGonderim),
                    "sayım ayı gün oranıyla"));

                if (s.TuketimKaynagi == TuketimKaynagi.Kullanim)
                    s.Adimlar.Add(new HesapAdimVm("−", "Sayımdan sonra raporlanan kullanım", N(s.SayimdanSonraKullanim)));
            }
            else if (s.StokKaynagi == StokKaynagi.KayitFarki)
            {
                s.Adimlar.Add(new HesapAdimVm("", "Bugüne kadar gönderilen", N(s.SayimdanSonraGonderim), "fiziki sayım yok"));
                s.Adimlar.Add(new HesapAdimVm("−", "Bugüne kadar raporlanan kullanım", N(s.SayimdanSonraKullanim)));
            }

            if (s.StokKaynagi != StokKaynagi.Yok)
            {
                if (s.TahminiTuketim > 0)
                    s.Adimlar.Add(new HesapAdimVm("−", $"Tahmini tüketim ({s.TahminGun} gün)", N(s.TahminiTuketim),
                        s.TuketimKaynagi == TuketimKaynagi.Kullanim
                            ? "kullanımı henüz raporlanmamış günler"
                            : "gönderim ortalamasından"));

                s.Adimlar.Add(new HesapAdimVm("=", "Bugünkü tahmini stok", N(stok) + " test",
                    s.KutuBirimli ? $"≈ {(decimal)stok / carpan:N1} kutu" : null));
            }

            if (s.OrtalamaAylikKullanim > 0)
            {
                s.Adimlar.Add(new HesapAdimVm("", "Aylık tüketim", N(s.OrtalamaAylikKullanim) + " test",
                    s.TuketimKaynagi == TuketimKaynagi.Kullanim
                        ? $"raporlanan son {s.OrtalamaAySayisi} ayın ortalaması"
                        : "son 6 ayın gönderim ortalaması"));

                if (s.StokKaynagi != StokKaynagi.Yok)
                {
                    s.Adimlar.Add(new HesapAdimVm("", $"Hedef stok ({hedefAy} ay)", N(s.HedefStok) + " test"));

                    if (s.Yolda > 0)
                        s.Adimlar.Add(new HesapAdimVm("", "Yolda (onaylı, gönderim kaydı bekleniyor)", N(s.Yolda) + " test"));

                    s.Adimlar.Add(new HesapAdimVm("=", "Önerilen sipariş",
                        s.OnerilenTest > 0
                            ? (s.KutuBirimli ? $"{s.OnerilenKutu} kutu ({N(s.OnerilenTest)} test)" : $"{N(s.OnerilenTest)} adet")
                            : "gerek yok",
                        s.OnerilenTest > 0 && s.KutuBirimli ? "kutuya yukarı yuvarlandı" : null));
                }
            }

            // ── Tek satır özet ──
            var parcalar = new List<string>();

            if (s.StokKaynagi == StokKaynagi.FizikiSayim)
            {
                var p = $"Sayım {N(s.SonSayimTestCount)} + gönderim {N(s.SayimdanSonraGonderim)}";
                if (s.TuketimKaynagi == TuketimKaynagi.Kullanim) p += $" − kullanım {N(s.SayimdanSonraKullanim)}";
                if (s.TahminiTuketim > 0) p += $" − tahmini {N(s.TahminiTuketim)}";
                parcalar.Add(p + $" = {N(stok)} test");
            }
            else if (s.StokKaynagi == StokKaynagi.KayitFarki)
            {
                var p = $"Sayım yok · gönderim {N(s.SayimdanSonraGonderim)} − kullanım {N(s.SayimdanSonraKullanim)}";
                if (s.TahminiTuketim > 0) p += $" − tahmini {N(s.TahminiTuketim)}";
                parcalar.Add(p + $" = {N(stok)} test");
            }
            else
            {
                parcalar.Add("Stok bilinmiyor");
            }

            if (s.OrtalamaAylikKullanim > 0)
            {
                parcalar.Add($"ayda ~{N(s.OrtalamaAylikKullanim)}");
                if (s.StokAy.HasValue) parcalar.Add($"{s.StokAy.Value.ToString("0.#", TR)} ay yeter");
            }

            s.HesaplamaOzeti = string.Join(" · ", parcalar);

            // ── Satır uyarıları ──
            if (stok < 0)
                s.Uyarilar.Add("Hesap stok eksi — gönderim kaydı eksik ya da kullanım fazla girilmiş olabilir.");

            if (s.Durum == StokDurumu.Durgun)
                s.Uyarilar.Add("Son raporlanan aylarda hiç kullanım yok ama elde stok var.");

            if (s.Yolda > 0)
                s.Uyarilar.Add($"{N(s.Yolda)} test onaylandı, gönderim kaydı henüz girilmemiş — öneriden düşüldü.");

            if (s.BekleyenTalep > 0)
                s.Uyarilar.Add($"Bu ürün için {N(s.BekleyenTalep)} testlik bekleyen talep var.");

            if (s.TrendYuzde.HasValue && Math.Abs(s.TrendYuzde.Value) >= 30)
                s.Uyarilar.Add(s.TrendYuzde.Value > 0
                    ? $"Tüketim önceki döneme göre %{s.TrendYuzde.Value:0} arttı."
                    : $"Tüketim önceki döneme göre %{Math.Abs(s.TrendYuzde.Value):0} azaldı.");
        }

        private void HastaneUyarilari(HastaneStokPaneliVm p, DateTime bugun, DateTime buAy)
        {
            if (!p.SayimVar)
            {
                p.Uyarilar.Add("Bu hastanenin fiziki sayımı yok. Stok, gönderim − kullanım kayıt farkından hesaplandı; güven düşük.");
            }
            else
            {
                var yas = (bugun - p.SonSayimTarihi!.Value.Date).Days;
                if (yas > Ayar.SayimEskiGun)
                    p.Uyarilar.Add($"Son sayım {yas} gün önce ({p.SonSayimTarihi:dd.MM.yyyy}). Kural 3 ayda bir sayım — yenisi planlanmalı.");
            }

            if (!p.KullanimSonAy.HasValue)
            {
                p.Uyarilar.Add("Bu hastane için hiç kullanım (istatistik) verisi girilmemiş. Tüketim, gönderim ortalamasından tahmin ediliyor.");
            }
            else if (p.KullanimSonAy.Value < buAy.AddMonths(-1))
            {
                p.Uyarilar.Add($"Kullanım verisi en son {p.KullanimSonAy.Value.ToString("MMMM yyyy", TR)} için girilmiş. " +
                               "Sonrası ortalamadan tahmin ediliyor — istatistik girişi tamamlanırsa rakamlar kesinleşir.");
            }

            if (p.GonderimSonAy.HasValue && p.GonderimSonAy.Value < buAy.AddMonths(-1))
            {
                p.Uyarilar.Add($"Gönderim kaydı en son {p.GonderimSonAy.Value.ToString("MMMM yyyy", TR)} için girilmiş. " +
                               "Sonraki gönderimler işlenmediyse stok olduğundan düşük görünür.");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  TÜM HASTANELER — ÖZET
        // ═══════════════════════════════════════════════════════════
        public async Task<StokOzetSayfaVm> OzetAsync(int hedefAy = 3, CancellationToken ct = default)
        {
            if (hedefAy <= 0) hedefAy = Ayar.HedefAy;

            var aktifIds = await _db.Hospitals.AsNoTracking()
                .Where(h => h.Aktif)
                .Select(h => h.HospitalID)
                .ToListAsync(ct);

            var paneller = await HesaplaTopluAsync(aktifIds, hedefAy, null, ct);

            var bekleyenHam = await _db.SiparisTalepler.AsNoTracking()
                .Where(t => t.Durum == SiparisTalepDurumu.Beklemede)
                .GroupBy(t => t.HospitalID)
                .Select(g => new { HospitalID = g.Key, Adet = g.Count() })
                .ToListAsync(ct);
            var bekleyen = bekleyenHam.ToDictionary(x => x.HospitalID, x => x.Adet);

            var vm = new StokOzetSayfaVm { HedefAy = hedefAy, TedarikGun = Ayar.TedarikGun };

            foreach (var p in paneller.Values)
            {
                // Hiç hesaplanabilir ürünü olmayan hastane listeyi kalabalıklaştırmasın
                var hesaplanan = p.Satirlar.Where(s => s.Durum != StokDurumu.VeriYok).ToList();
                if (hesaplanan.Count == 0 && !bekleyen.ContainsKey(p.HospitalID)) continue;

                vm.Hastaneler.Add(new HastaneStokOzetVm
                {
                    HospitalID = p.HospitalID,
                    HospitalName = p.HospitalName,
                    SonSayimTarihi = p.SonSayimTarihi,
                    SayimGun = p.GunlerdenSayimdanBeri,
                    KullanimSonAy = p.KullanimSonAy,
                    GonderimSonAy = p.GonderimSonAy,
                    Guven = p.Guven,
                    UrunSayisi = hesaplanan.Count,
                    KritikSayisi = p.KritikSayisi,
                    DusukSayisi = p.DusukSayisi,
                    DurgunSayisi = p.DurgunSayisi,
                    OnerilenKalem = p.OnerilenKalemSayisi,
                    OnerilenKutu = p.OnerilenToplamKutu,
                    BekleyenTalepSayisi = bekleyen.TryGetValue(p.HospitalID, out var b) ? b : 0,
                    Uyarilar = p.Uyarilar
                });
            }

            vm.Hastaneler = vm.Hastaneler
                .OrderByDescending(h => h.OncelikPuani)
                .ThenBy(h => h.HospitalName)
                .ToList();

            return vm;
        }

        // ═══════════════════════════════════════════════════════════
        //  SİPARİŞ KARARI
        // ═══════════════════════════════════════════════════════════
        //
        //  Karşılaştırma KUTU bazındadır. Eski hesap testleri kıyaslıyordu:
        //  ihtiyaç 30 test, hastane 1 kutu (100 test) istemiş → "%233 sapma,
        //  kırmızı". Oysa 1 kutudan azı sipariş edilemez.
        //
        //  Eşikler:  ±%10 uygun · ±%10–30 incele · >%30 sorunlu
        //  Ek kural: 1 kutuluk fark, o kutu 1 aydan az stok ekliyorsa
        //            yuvarlama payı sayılır.
        //  Güven düşükse kırmızı verilmez; en fazla "incele".
        // ═══════════════════════════════════════════════════════════
        public SiparisKararVm Degerlendir(
            StokHesapSatirVm? s, int talepTest, int oncekiBekleyenTest = 0, int? hedefAy = null)
        {
            var hedef = hedefAy.GetValueOrDefault(Ayar.HedefAy);
            if (hedef <= 0) hedef = Ayar.HedefAy;

            var k = new SiparisKararVm();

            // ── Eşleşmedi ──
            if (s == null)
            {
                k.Renk = DogrulamaRengi.VeriYok;
                k.Baslik = "Eşleşmedi";
                k.Gerekce = "Ürün kataloğumuzdaki bir testle eşleşmedi; stok ve tüketim verisine ulaşılamıyor.";
                return k;
            }

            var carpan = Math.Max(1, s.BoxTextCount);
            var birim = s.Birim;
            var ort = s.OrtalamaAylikKullanim;
            var talepMiktar = (decimal)talepTest / carpan;

            string Ay(decimal v) => v.ToString("0.#", TR);
            string M(decimal v) => v.ToString("0.##", TR);

            // ── Tüketim yok ──
            if (ort <= 0)
            {
                if (s.TuketimKaynagi == TuketimKaynagi.Kullanim && talepTest > 0)
                {
                    k.Renk = s.Guven == VeriGuveni.Dusuk ? DogrulamaRengi.Sari : DogrulamaRengi.Kirmizi;
                    k.Baslik = "Kullanım yok";
                    k.Gerekce = $"Raporlanan son {s.OrtalamaAySayisi} ayda bu üründe hiç kullanım görünmüyor"
                              + (s.HesapGuncelStok > 0 ? $", elde {s.HesapGuncelStok.ToString("N0", TR)} test var." : ".");
                    return k;
                }

                k.Renk = DogrulamaRengi.VeriYok;
                k.Baslik = "Veri yok";
                k.Gerekce = "Bu ürün için tüketim ya da gönderim geçmişi yok; karşılaştırma yapılamadı.";
                return k;
            }

            // ── Stok bilinmiyor: yalnızca geçmişle kıyas ──
            if (s.StokKaynagi == StokKaynagi.Yok)
            {
                var kacAylik = talepTest / ort;
                k.SiparisSonrasiAy = Math.Round(kacAylik, 1);

                if (kacAylik <= hedef + 0.5m)
                {
                    k.Renk = DogrulamaRengi.Yesil;
                    k.Baslik = "Geçmişle uyumlu";
                }
                else if (kacAylik <= hedef * 2m)
                {
                    k.Renk = DogrulamaRengi.Sari;
                    k.Baslik = "Geçmişe göre yüksek";
                }
                else
                {
                    k.Renk = DogrulamaRengi.Sari;   // stok bilinmeden kırmızı verilmez
                    k.Baslik = "Geçmişe göre çok yüksek";
                }

                k.Gerekce = $"Sayımı yok. Son 6 ayda aylık ortalama gönderim {M(ort / carpan)} {birim}; "
                          + $"istenen {M(talepMiktar)} {birim} yaklaşık {Ay(kacAylik)} aylık ihtiyaca denk.";
                return k;
            }

            // ── Asıl karşılaştırma ──
            var eldeki = Math.Max(0, s.HesapGuncelStok) + s.Yolda + Math.Max(0, oncekiBekleyenTest);
            var (oneriTest, oneriKutu) = IhtiyacHesapla(ort, hedef, eldeki, carpan);

            k.OnerilenTest = oneriTest;
            k.OnerilenKutu = oneriKutu;
            k.Fark = talepMiktar - oneriKutu;
            k.SiparisSonrasiAy = Math.Round((eldeki + talepTest) / ort, 1);

            var eldekiAy = eldeki / ort;
            var stokCumle = eldeki <= 0
                ? "Stok tükenmiş görünüyor"
                : $"Stok {Ay(eldekiAy)} ay yetiyor";
            var ekNot = "";
            if (s.Yolda > 0) ekNot += $" {s.Yolda.ToString("N0", TR)} test yolda.";
            if (oncekiBekleyenTest > 0) ekNot += $" Aynı ürün için daha önce gelmiş bekleyen talep var ({M((decimal)oncekiBekleyenTest / carpan)} {birim}).";

            if (oneriKutu == 0)
            {
                if (talepTest <= 0)
                {
                    k.Renk = DogrulamaRengi.Yesil;
                    k.Baslik = "Uygun";
                    k.Gerekce = $"Stok {Ay(eldekiAy)} ay yetiyor; sipariş gerekmiyor.";
                }
                else if (k.SiparisSonrasiAy <= hedef + 1)
                {
                    k.Renk = DogrulamaRengi.Sari;
                    k.Baslik = "Gerek yok";
                    k.Gerekce = $"Stok zaten {Ay(eldekiAy)} ay yetiyor (hedef {hedef} ay). "
                              + $"Bu siparişle {Ay(k.SiparisSonrasiAy.Value)} ay olur.";
                }
                else
                {
                    k.Renk = DogrulamaRengi.Kirmizi;
                    k.Baslik = "Fazla";
                    k.Gerekce = $"Stok zaten {Ay(eldekiAy)} ay yetiyor (hedef {hedef} ay). "
                              + $"Bu siparişle {Ay(k.SiparisSonrasiAy.Value)} aylık stok birikir.";
                }
            }
            else
            {
                var sapma = k.Fark / oneriKutu * 100m;
                k.SapmaYuzde = Math.Round(sapma, 1);

                // 1 kutuluk fark, o kutu en çok 1 aylık stok ekliyorsa yuvarlama payıdır
                var birKutuAy = carpan / ort;
                var yuvarlamaPayi = s.KutuBirimli && Math.Abs(k.Fark) <= 1m && birKutuAy <= 1m;

                if (Math.Abs(sapma) <= 10m || yuvarlamaPayi)
                {
                    k.Renk = DogrulamaRengi.Yesil;
                    k.Baslik = "Uygun";
                }
                else if (Math.Abs(sapma) <= 30m)
                {
                    k.Renk = DogrulamaRengi.Sari;
                    k.Baslik = sapma > 0 ? "Biraz fazla" : "Biraz az";
                }
                else
                {
                    k.Renk = DogrulamaRengi.Kirmizi;
                    k.Baslik = sapma > 0 ? "Fazla" : "Az";
                }

                k.Gerekce = $"{stokCumle}; {hedef} aylık hedef için {oneriKutu} {birim} gerekiyor, "
                          + $"{M(talepMiktar)} {birim} istenmiş. Bu siparişle {Ay(k.SiparisSonrasiAy.Value)} ay olur.";
            }

            k.Gerekce += ekNot;

            // Güven düşükse kırmızı verme
            if (s.Guven == VeriGuveni.Dusuk && k.Renk == DogrulamaRengi.Kirmizi)
            {
                k.Renk = DogrulamaRengi.Sari;
                k.Gerekce += $" Veri güveni düşük ({s.GuvenNedeni?.ToLower(TR)}) — kesin hüküm verilmedi.";
            }

            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════
        private static DateTime AyBasi(DateTime d) => new DateTime(d.Year, d.Month, 1);

        private static Dictionary<DateTime, int> AylikTopla(IEnumerable<Hareket> hareketler)
        {
            var d = new Dictionary<DateTime, int>();
            foreach (var h in hareketler)
            {
                d.TryGetValue(h.Ay, out var mevcut);
                d[h.Ay] = mevcut + h.Adet;
            }
            return d;
        }

        private static int DurumSirasi(StokDurumu d) => d switch
        {
            StokDurumu.Kritik => 0,
            StokDurumu.Dusuk => 1,
            StokDurumu.Izlemede => 2,
            StokDurumu.Saglikli => 3,
            StokDurumu.Durgun => 4,
            _ => 5
        };

        private class TestBilgi
        {
            public int TestID { get; set; }
            public string TestName { get; set; } = "";
            public string? SutCode { get; set; }
            public string? Kategori { get; set; }
            public string? Platform { get; set; }
            public TestGirisTipi GirisTipi { get; set; }
            public int? KutuBasinaAdet { get; set; }
        }

        private class SnapBilgi
        {
            public int HospitalID { get; set; }
            public int? MappedTestID { get; set; }
            public int? EtkinTestID { get; set; }
            public string ReferenceNumber { get; set; } = "";
            public string ProductName { get; set; } = "";
            public string? Platform { get; set; }
            public UrunTipi UrunTipi { get; set; }
            public int SayimNo { get; set; }
            public DateTime SayimTarihi { get; set; }
            public int TestCount { get; set; }
            public int BoxTextCount { get; set; }
        }

        private class Hareket
        {
            public int HospitalID { get; set; }
            public int TestID { get; set; }
            public DateTime Ay { get; set; }
            public int Adet { get; set; }
        }

        private class AcikKalem
        {
            public int HospitalID { get; set; }
            public int TestID { get; set; }
            public SiparisTalepDurumu Durum { get; set; }
            public DateTime? OnayTarihi { get; set; }
            public int TalepTest { get; set; }
            public int? OnayTest { get; set; }
        }
    }
}
