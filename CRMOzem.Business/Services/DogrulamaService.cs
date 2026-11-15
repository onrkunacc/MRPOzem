// ═══════════════════════════════════════════════════════════════════════
//  CRMOzem.Business/Services/DogrulamaService.cs
//
//  Sipariş doğrulama iş katmanı.
//
//  v4 — Öneri ve hüküm artık CANLI:
//    · Bekleyen talep her açılışta güncel sayım/istatistikle yeniden
//      hesaplanır. Eskiden öneri, siparişin çekildiği andaki haliyle
//      donuyordu; sonradan sayım girilse de ekran değişmiyordu.
//    · Onaylanan/reddedilen talep, karar anındaki rakamlarla saklanır.
//    · Karşılaştırma KUTU bazında; hesabın tamamı StokHesaplamaService'te.
//    · Aynı hastanenin aynı ürün için birden çok bekleyen talebi varsa
//      sonradan gelen, öncekini "yolda" sayarak değerlendirilir.
// ═══════════════════════════════════════════════════════════════════════

using CRMOzem.Business.Helpers;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.Core.ViewModels.Dogrulama;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CRMOzem.Business.Services
{
    public class DogrulamaService : IDogrulamaService
    {
        private readonly ApplicationDbContext _db;
        private readonly IStokHesaplamaService _stok;

        public DogrulamaService(ApplicationDbContext db, IStokHesaplamaService stok)
        {
            _db = db;
            _stok = stok;
        }

        private static string? DonemToStr(DateTime? d) =>
            d.HasValue ? $"{d.Value.Year:D4}-{d.Value.Month:D2}" : null;

        // ═══════════════════════════════════════════════════════════
        //  CANLI HESAP — bekleyen talepler
        // ═══════════════════════════════════════════════════════════

        /// <summary>Bir hesap turunun bellekteki sonucu.</summary>
        private class Baglam
        {
            public Dictionary<int, HastaneStokPaneliVm> Paneller { get; } = new();
            public Dictionary<int, StokHesapSatirVm> SatirByKalem { get; } = new();
            public Dictionary<int, SiparisKararVm> KararByKalem { get; } = new();
            public Dictionary<int, int> OncekiByKalem { get; } = new();
            public List<SiparisTalep> Bekleyenler { get; set; } = new();
            public int Guncellenen { get; set; }
        }

        public async Task<int> YenidenHesaplaAsync(IEnumerable<int>? talepIds, CancellationToken ct)
        {
            var b = await HesaplaVeYazAsync(talepIds, ct);
            return b.Guncellenen;
        }

        /// <summary>
        /// İlgili hastanelerin TÜM bekleyen taleplerini hesaplar ve kalemlere yazar.
        /// (Tek talep istense bile hastanenin diğer bekleyenleri de yüklenir —
        /// "önceki bekleyen talep" kuralı doğru işlesin diye.)
        /// </summary>
        private async Task<Baglam> HesaplaVeYazAsync(IEnumerable<int>? talepIds, CancellationToken ct)
        {
            var b = new Baglam();

            var q = _db.SiparisTalepler
                .Include(t => t.Kalemler)
                .Where(t => t.Durum == SiparisTalepDurumu.Beklemede);

            if (talepIds != null)
            {
                var idList = talepIds.Distinct().ToList();
                if (idList.Count == 0) return b;

                var hastaneIds = await _db.SiparisTalepler.AsNoTracking()
                    .Where(t => idList.Contains(t.ID))
                    .Select(t => t.HospitalID)
                    .Distinct()
                    .ToListAsync(ct);

                if (hastaneIds.Count == 0) return b;
                q = q.Where(t => hastaneIds.Contains(t.HospitalID));
            }

            b.Bekleyenler = await q.ToListAsync(ct);
            if (b.Bekleyenler.Count == 0) return b;

            var hIds = b.Bekleyenler.Select(t => t.HospitalID).Distinct().ToList();

            var ekUrunler = b.Bekleyenler
                .SelectMany(t => t.Kalemler
                    .Where(k => k.TestID.HasValue)
                    .Select(k => (t.HospitalID, k.TestID!.Value)))
                .Distinct()
                .ToList();

            var paneller = await _stok.HesaplaTopluAsync(hIds, _stok.Ayar.HedefAy, ekUrunler, ct);
            foreach (var kv in paneller) b.Paneller[kv.Key] = kv.Value;

            foreach (var grup in b.Bekleyenler.GroupBy(t => t.HospitalID))
            {
                paneller.TryGetValue(grup.Key, out var panel);

                var satirByTest = (panel?.Satirlar ?? new List<StokHesapSatirVm>())
                    .Where(s => s.TestID.HasValue)
                    .GroupBy(s => s.TestID!.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                // Aynı üründe önce gelen talep, sonrakinin "eldeki" miktarına eklenir
                var oncekiBekleyen = new Dictionary<int, int>();

                foreach (var talep in grup.OrderBy(t => t.TalepTarihi).ThenBy(t => t.ID))
                {
                    foreach (var kalem in talep.Kalemler.OrderBy(k => k.ID))
                    {
                        StokHesapSatirVm? satir = null;
                        var onceki = 0;

                        if (kalem.TestID.HasValue)
                        {
                            satirByTest.TryGetValue(kalem.TestID.Value, out satir);
                            oncekiBekleyen.TryGetValue(kalem.TestID.Value, out onceki);
                        }

                        var karar = _stok.Degerlendir(satir, kalem.HastaneTalepTest, onceki);

                        if (satir != null) b.SatirByKalem[kalem.ID] = satir;
                        b.KararByKalem[kalem.ID] = karar;
                        b.OncekiByKalem[kalem.ID] = onceki;

                        if (KalemeYaz(kalem, satir, karar)) b.Guncellenen++;

                        if (kalem.TestID.HasValue)
                            oncekiBekleyen[kalem.TestID.Value] = onceki + kalem.HastaneTalepTest;
                    }
                }
            }

            if (b.Guncellenen > 0)
                await _db.SaveChangesAsync(ct);

            return b;
        }

        /// <summary>Hesap sonucunu kalemin saklanan alanlarına işler. Değişiklik varsa true.</summary>
        private static bool KalemeYaz(SiparisTalepKalem k, StokHesapSatirVm? satir, SiparisKararVm karar)
        {
            var stok = satir?.HesapGuncelStok ?? 0;
            var ort = satir?.OrtalamaAylikKullanim ?? 0m;

            decimal? sapma = karar.SapmaYuzde.HasValue
                ? Math.Clamp(Math.Round(karar.SapmaYuzde.Value, 2), -9999m, 9999m)
                : null;

            var aciklama = $"{karar.Baslik} — {karar.Gerekce}";
            if (satir != null && !string.IsNullOrEmpty(satir.HesaplamaOzeti))
                aciklama += $" [{satir.HesaplamaOzeti}]";
            if (aciklama.Length > 2000) aciklama = aciklama.Substring(0, 2000);

            var degisti =
                k.HesapGuncelStok != stok
                || k.OrtalamaAylikKullanim != ort
                || k.OnerilenTest != karar.OnerilenTest
                || k.OnerilenKutu != karar.OnerilenKutu
                || k.SapmaYuzde != sapma
                || k.DogrulamaRengi != karar.Renk
                || k.HesaplamaAciklamasi != aciklama;

            if (!degisti) return false;

            k.HesapGuncelStok = stok;
            k.OrtalamaAylikKullanim = ort;
            k.OnerilenTest = karar.OnerilenTest;
            k.OnerilenKutu = karar.OnerilenKutu;
            k.SapmaYuzde = sapma;
            k.DogrulamaRengi = karar.Renk;
            k.HesaplamaAciklamasi = aciklama;
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  LİSTE
        // ═══════════════════════════════════════════════════════════
        public async Task<DogrulamaIndexVm> GetListAsync(DogrulamaFiltreVm filtre, CancellationToken ct)
        {
            // Önce bekleyenleri güncel veriyle tazele
            var baglam = await HesaplaVeYazAsync(null, ct);

            var q = _db.SiparisTalepler
                .AsNoTracking()
                .Include(t => t.Hospital)
                .Include(t => t.Kalemler)
                .AsQueryable();

            if (filtre.Tumu)
            {
                // durum süzgeci yok
            }
            else if (filtre.Onaylananlar)
            {
                q = q.Where(t => t.Durum == SiparisTalepDurumu.OnaylandiAynen
                              || t.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi);
            }
            else if (filtre.Durum.HasValue)
            {
                q = q.Where(t => t.Durum == filtre.Durum.Value);
            }

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

            // ── Açılır kaynakları ──
            var tumDonemlerRaw = await _db.SiparisTalepler
                .AsNoTracking()
                .Where(t => t.HedefDonem.HasValue)
                .Select(t => t.HedefDonem!.Value)
                .Distinct()
                .ToListAsync(ct);

            var tumDonemler = tumDonemlerRaw
                .Select(d => $"{d.Year:D4}-{d.Month:D2}")
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            // Yalnızca talebi olan hastaneler — 100 hastanelik boş liste yerine
            var talepliHastaneIds = await _db.SiparisTalepler.AsNoTracking()
                .Select(t => t.HospitalID)
                .Distinct()
                .ToListAsync(ct);

            var tumHastaneler = await _db.Hospitals
                .AsNoTracking()
                .Where(h => talepliHastaneIds.Contains(h.HospitalID))
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

            // ── Satırlar ──
            var satirlar = talepler.Select(t =>
            {
                var kirmizi = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Kirmizi);
                var sari = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Sari);
                var yesil = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.Yesil);
                var veriYok = t.Kalemler.Count(k => k.DogrulamaRengi == DogrulamaRengi.VeriYok);

                DogrulamaRengi enKotu;
                if (kirmizi > 0) enKotu = DogrulamaRengi.Kirmizi;
                else if (sari > 0) enKotu = DogrulamaRengi.Sari;
                else if (veriYok > 0) enKotu = DogrulamaRengi.VeriYok;
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
                    OnayTarihi = t.OnayTarihi,
                    OnaylayanKullanici = t.OnaylayanKullanici,
                    KalemSayisi = t.Kalemler.Count,
                    ToplamTalepTest = t.Kalemler.Sum(k => k.HastaneTalepTest),
                    ToplamOneriTest = t.Kalemler.Sum(k => k.OnerilenTest),
                    EnKotuRenk = enKotu,
                    KirmiziSayisi = kirmizi,
                    SariSayisi = sari,
                    YesilSayisi = yesil,
                    VeriYokSayisi = veriYok,
                    EslesmeyenSayisi = t.Kalemler.Count(k => !k.TestID.HasValue),
                    HastaneNotu = t.HastaneNotu,
                    Guven = baglam.Paneller.TryGetValue(t.HospitalID, out var p) ? p.Guven : VeriGuveni.Yok
                };
            }).ToList();

            if (filtre.EnKotuRenk.HasValue)
                satirlar = satirlar.Where(s => s.EnKotuRenk == filtre.EnKotuRenk.Value).ToList();

            // ── Durum sayıları — süzgeçten bağımsız ──
            var genelDurumSayilari = await _db.SiparisTalepler
                .AsNoTracking()
                .GroupBy(t => t.Durum)
                .Select(g => new { Durum = g.Key, Adet = g.Count() })
                .ToListAsync(ct);

            int Say(SiparisTalepDurumu d) => genelDurumSayilari.FirstOrDefault(x => x.Durum == d)?.Adet ?? 0;

            // ── Sayfalama ──
            var boy = filtre.SayfaBoyu is 25 or 50 or 100 ? filtre.SayfaBoyu : 25;
            var toplamSatir = satirlar.Count;
            var sayfaSayisi = Math.Max(1, (int)Math.Ceiling(toplamSatir / (double)boy));
            var sayfa = Math.Clamp(filtre.Sayfa, 1, sayfaSayisi);
            filtre.Sayfa = sayfa;
            filtre.SayfaBoyu = boy;

            var vm = new DogrulamaIndexVm
            {
                Satirlar = satirlar.Skip((sayfa - 1) * boy).Take(boy).ToList(),
                Sayfa = sayfa,
                SayfaBoyu = boy,
                ToplamSatir = toplamSatir,
                Filtre = filtre,
                ToplamBekleyen = Say(SiparisTalepDurumu.Beklemede),
                ToplamOnaylananAynen = Say(SiparisTalepDurumu.OnaylandiAynen),
                ToplamOnaylananDuzeltildi = Say(SiparisTalepDurumu.OnaylandiDuzeltildi),
                ToplamReddedilen = Say(SiparisTalepDurumu.Reddedildi),
                ToplamKirmizi = satirlar.Count(s => s.KirmiziSayisi > 0),
                ToplamSari = satirlar.Count(s => s.SariSayisi > 0 && s.KirmiziSayisi == 0)
            };

            BekleyenOzetiDoldur(vm, baglam);

            var eskiSinir = DateTime.Today.AddDays(-15);
            vm.EskiBekleyen = baglam.Bekleyenler.Count(t => t.TalepTarihi < eskiSinir);
            return vm;
        }

        /// <summary>Bekleyen taleplerin kalem bazlı özeti — sayfa başındaki rakamlar.</summary>
        private static void BekleyenOzetiDoldur(DogrulamaIndexVm vm, Baglam b)
        {
            var eslesmeyenler = new List<(string Ad, string Ref, int HospitalID)>();

            foreach (var talep in b.Bekleyenler)
            {
                var tumuYesil = talep.Kalemler.Count > 0;

                foreach (var k in talep.Kalemler)
                {
                    vm.BekleyenKalem++;

                    switch (k.DogrulamaRengi)
                    {
                        case DogrulamaRengi.Yesil: vm.UygunKalem++; break;
                        case DogrulamaRengi.Sari: vm.InceleKalem++; break;
                        case DogrulamaRengi.Kirmizi: vm.SorunluKalem++; break;
                        default: vm.VeriYokKalem++; break;
                    }

                    if (k.DogrulamaRengi != DogrulamaRengi.Yesil) tumuYesil = false;

                    if (!k.TestID.HasValue)
                    {
                        vm.EslesmeyenKalem++;
                        eslesmeyenler.Add((k.ProductName, k.ReferenceNumber, talep.HospitalID));
                    }

                    // Fazla / eksik istenen: yalnızca stoku bilinen kutu birimli ürünlerde anlamlı
                    if (b.SatirByKalem.TryGetValue(k.ID, out var satir)
                        && b.KararByKalem.TryGetValue(k.ID, out var karar)
                        && satir.KutuBirimli
                        && satir.StokKaynagi != StokKaynagi.Yok
                        && satir.OrtalamaAylikKullanim > 0)
                    {
                        if (karar.Fark > 0) vm.FazlaIstenenKutu += karar.Fark;
                        else if (karar.Fark < 0) vm.EksikIstenenKutu += -karar.Fark;
                    }
                }

                if (tumuYesil) vm.HemenOnaylanabilir++;
            }

            vm.FazlaIstenenKutu = Math.Round(vm.FazlaIstenenKutu, 0);
            vm.EksikIstenenKutu = Math.Round(vm.EksikIstenenKutu, 0);

            vm.EslesmeyenUrunler = eslesmeyenler
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Ref)
                    ? "AD:" + ReferansKodu.AdNormalize(x.Ad)
                    : "REF:" + ReferansKodu.Normalize(x.Ref))
                .Select(g => new EslesmeyenUrunVm
                {
                    ProductName = g.First().Ad,
                    ReferenceNumber = g.First().Ref,
                    KalemSayisi = g.Count(),
                    HastaneSayisi = g.Select(x => x.HospitalID).Distinct().Count()
                })
                .OrderByDescending(x => x.KalemSayisi)
                .ThenBy(x => x.ProductName)
                .Take(25)
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════
        //  DETAY
        // ═══════════════════════════════════════════════════════════
        public async Task<DogrulamaDetayVm?> GetDetayAsync(int talepId, CancellationToken ct)
        {
            var durum = await _db.SiparisTalepler.AsNoTracking()
                .Where(x => x.ID == talepId)
                .Select(x => (SiparisTalepDurumu?)x.Durum)
                .FirstOrDefaultAsync(ct);

            if (durum == null) return null;

            // Bekleyen talep: güncel veriyle yeniden hesapla
            Baglam? b = null;
            if (durum == SiparisTalepDurumu.Beklemede)
                b = await HesaplaVeYazAsync(new[] { talepId }, ct);

            var t = await _db.SiparisTalepler
                .AsNoTracking()
                .Include(x => x.Hospital)
                .Include(x => x.Kalemler)
                .FirstOrDefaultAsync(x => x.ID == talepId, ct);

            if (t == null) return null;

            var testler = await TestleriGetirAsync(t.Kalemler, ct);

            HastaneStokPaneliVm? panel = null;
            b?.Paneller.TryGetValue(t.HospitalID, out panel);

            var vm = new DogrulamaDetayVm
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
                HedefAy = _stok.Ayar.HedefAy,
                SonSayimTarihi = panel?.SonSayimTarihi,
                KullanimSonAy = panel?.KullanimSonAy,
                Guven = panel?.Guven ?? VeriGuveni.Yok,
                HastaneUyarilari = panel?.Uyarilar ?? new List<string>()
            };

            foreach (var k in t.Kalemler)
            {
                StokHesapSatirVm? satir = null;
                SiparisKararVm? karar = null;
                var onceki = 0;

                if (b != null)
                {
                    b.SatirByKalem.TryGetValue(k.ID, out satir);
                    b.KararByKalem.TryGetValue(k.ID, out karar);
                    b.OncekiByKalem.TryGetValue(k.ID, out onceki);
                }

                testler.TryGetValue(k.TestID ?? 0, out var test);
                var (carpan, kutuBirimli) = KalemCarpani(k, test, satir);

                var kalemVm = new DogrulamaKalemVm
                {
                    KalemID = k.ID,
                    ExternalProductId = k.ExternalProductId.ToString(),
                    ProductName = k.ProductName,
                    ReferenceNumber = k.ReferenceNumber,
                    TestID = k.TestID,
                    TestAdi = test?.TestName,
                    UrunTipi = satir?.UrunTipi ?? k.UrunTipi,
                    Carpan = carpan,
                    KutuBirimli = kutuBirimli,
                    HastaneTalepTest = k.HastaneTalepTest,
                    HastaneTalepKutu = k.HastaneTalepKutu,
                    HesapGuncelStok = k.HesapGuncelStok,
                    OrtalamaAylikKullanim = k.OrtalamaAylikKullanim,
                    OnerilenTest = k.OnerilenTest,
                    OnerilenKutu = k.OnerilenKutu,
                    SapmaYuzde = k.SapmaYuzde,
                    DogrulamaRengi = k.DogrulamaRengi,
                    OnaylananTest = k.OnaylananTest ?? k.HastaneTalepTest,
                    KalemNotu = k.KalemNotu,
                    OncekiBekleyenTest = onceki
                };

                if (karar != null)
                {
                    // Canlı hüküm
                    kalemVm.KararBaslik = karar.Baslik;
                    kalemVm.Gerekce = karar.Gerekce;
                    kalemVm.SiparisSonrasiAy = karar.SiparisSonrasiAy;
                    kalemVm.HesaplamaAciklamasi = satir?.HesaplamaOzeti;
                }
                else
                {
                    // Karara bağlanmış talep: saklanan metin
                    kalemVm.KararBaslik = k.DogrulamaRengi switch
                    {
                        DogrulamaRengi.Yesil => "Uygun",
                        DogrulamaRengi.Sari => "İncele",
                        DogrulamaRengi.Kirmizi => "Sorunlu",
                        _ => "Veri yok"
                    };
                    kalemVm.Gerekce = k.HesaplamaAciklamasi;
                }

                if (satir != null)
                {
                    kalemVm.StokAy = satir.StokAy;
                    kalemVm.Yolda = satir.Yolda;
                    kalemVm.StokDurumu = satir.Durum;
                    kalemVm.StokKaynagi = satir.StokKaynagi;
                    kalemVm.Guven = satir.Guven;
                    kalemVm.GuvenNedeni = satir.GuvenNedeni;
                    kalemVm.TrendYuzde = satir.TrendYuzde;
                    kalemVm.Adimlar = satir.Adimlar;
                }

                vm.Kalemler.Add(kalemVm);
            }

            // Sorunlular üstte: kırmızı → sarı → veri yok → yeşil
            vm.Kalemler = vm.Kalemler
                .OrderBy(k => k.DogrulamaRengi switch
                {
                    DogrulamaRengi.Kirmizi => 0,
                    DogrulamaRengi.Sari => 1,
                    DogrulamaRengi.VeriYok => 2,
                    _ => 3
                })
                .ThenBy(k => k.ProductName)
                .ToList();

            // ── Siparişte olmayan ama stoku kritik/düşük ürünler ──
            if (b != null && panel != null)
            {
                var bekleyenTestler = b.Bekleyenler
                    .Where(x => x.HospitalID == t.HospitalID)
                    .SelectMany(x => x.Kalemler)
                    .Where(k => k.TestID.HasValue)
                    .Select(k => k.TestID!.Value)
                    .ToHashSet();

                vm.EksikKritikler = panel.Satirlar
                    .Where(s => s.TestID.HasValue
                             && (s.Durum == StokDurumu.Kritik || s.Durum == StokDurumu.Dusuk)
                             && s.OnerilenTest > 0
                             && !bekleyenTestler.Contains(s.TestID.Value))
                    .Take(20)
                    .ToList();

                vm.DigerBekleyenTalep = b.Bekleyenler.Count(x => x.HospitalID == t.HospitalID && x.ID != t.ID);
            }

            return vm;
        }

        // ═══════════════════════════════════════════════════════════
        //  ONAYLA — AYNEN
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool ok, string msg)> OnaylaAynenAsync(
            int talepId, string? onayNotu, string? kullanici, CancellationToken ct)
        {
            // Karar anındaki rakamları dondur
            await HesaplaVeYazAsync(new[] { talepId }, ct);

            var t = await _db.SiparisTalepler
                .Include(x => x.Kalemler)
                .FirstOrDefaultAsync(x => x.ID == talepId, ct);

            if (t == null) return (false, "Talep bulunamadı.");
            if (t.Durum != SiparisTalepDurumu.Beklemede)
                return (false, $"Bu talep zaten '{DurumAdi(t.Durum)}' durumunda.");

            foreach (var k in t.Kalemler)
                k.OnaylananTest = k.HastaneTalepTest;

            t.Durum = SiparisTalepDurumu.OnaylandiAynen;
            t.OnayTarihi = DateTime.Now;
            t.OnaylayanKullanici = kullanici;
            t.OnayNotu = onayNotu;
            t.GuncellemeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return (true, "Talep, hastanenin istediği miktarlarla onaylandı.");
        }

        // ═══════════════════════════════════════════════════════════
        //  ONAYLA — ekrandaki miktarlarla
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool ok, string msg)> OnaylaDuzeltilmisAsync(
            OnayPostDto dto, string? kullanici, CancellationToken ct)
        {
            // Karar anındaki rakamları dondur; çarpanlar ekrandakiyle aynı olsun
            var baglam = await HesaplaVeYazAsync(new[] { dto.TalepID }, ct);

            var t = await _db.SiparisTalepler
                .Include(x => x.Kalemler)
                .FirstOrDefaultAsync(x => x.ID == dto.TalepID, ct);

            if (t == null) return (false, "Talep bulunamadı.");
            if (t.Durum != SiparisTalepDurumu.Beklemede)
                return (false, $"Bu talep zaten '{DurumAdi(t.Durum)}' durumunda.");

            var testler = await TestleriGetirAsync(t.Kalemler, ct);

            var kalemMap = (dto.Kalemler ?? new List<KalemOnayDto>())
                .GroupBy(x => x.KalemID)
                .ToDictionary(g => g.Key, g => g.First());

            var degisenSayisi = 0;

            foreach (var k in t.Kalemler)
            {
                var yeni = k.HastaneTalepTest;

                if (kalemMap.TryGetValue(k.ID, out var girdi))
                {
                    var miktar = OndalikOku(girdi.OnaylananMiktar);

                    if (miktar.HasValue)
                    {
                        baglam.SatirByKalem.TryGetValue(k.ID, out var satir);
                        testler.TryGetValue(k.TestID ?? 0, out var test);
                        var (carpan, _) = KalemCarpani(k, test, satir);

                        yeni = (int)Math.Round(miktar.Value * carpan, MidpointRounding.AwayFromZero);
                    }
                    else if (girdi.OnaylananTest.HasValue)
                    {
                        yeni = girdi.OnaylananTest.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(girdi.KalemNotu))
                    {
                        var not = girdi.KalemNotu.Trim();
                        k.KalemNotu = not.Length > 500 ? not.Substring(0, 500) : not;
                    }
                }

                if (yeni < 0) yeni = 0;
                if (yeni != k.HastaneTalepTest) degisenSayisi++;
                k.OnaylananTest = yeni;
            }

            t.Durum = degisenSayisi > 0
                ? SiparisTalepDurumu.OnaylandiDuzeltildi
                : SiparisTalepDurumu.OnaylandiAynen;

            t.OnayTarihi = DateTime.Now;
            t.OnaylayanKullanici = kullanici;
            t.OnayNotu = dto.OnayNotu;
            t.GuncellemeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);

            return (true, degisenSayisi > 0
                ? $"Talep onaylandı — {degisenSayisi} kalemin miktarı düzeltildi."
                : "Talep, hastanenin istediği miktarlarla onaylandı.");
        }

        // ═══════════════════════════════════════════════════════════
        //  REDDET
        // ═══════════════════════════════════════════════════════════
        public async Task<(bool ok, string msg)> ReddetAsync(
            int talepId, string? redNedeni, string? kullanici, CancellationToken ct)
        {
            await HesaplaVeYazAsync(new[] { talepId }, ct);

            var t = await _db.SiparisTalepler.FirstOrDefaultAsync(x => x.ID == talepId, ct);

            if (t == null) return (false, "Talep bulunamadı.");
            if (t.Durum != SiparisTalepDurumu.Beklemede)
                return (false, $"Bu talep zaten '{DurumAdi(t.Durum)}' durumunda.");

            t.Durum = SiparisTalepDurumu.Reddedildi;
            t.OnayTarihi = DateTime.Now;
            t.OnaylayanKullanici = kullanici;
            t.OnayNotu = redNedeni;
            t.GuncellemeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return (true, "Talep reddedildi.");
        }

        // ═══════════════════════════════════════════════════════════
        //  ESKİ BEKLEYENLERİ KAPAT
        // ═══════════════════════════════════════════════════════════
        public async Task<int> EskiBekleyenleriKapatAsync(int gunEski, string? kullanici, CancellationToken ct)
        {
            if (gunEski < 1) gunEski = 1;
            var sinir = DateTime.Today.AddDays(-gunEski);

            var eskiler = await _db.SiparisTalepler
                .Where(t => t.Durum == SiparisTalepDurumu.Beklemede && t.TalepTarihi < sinir)
                .ToListAsync(ct);

            foreach (var t in eskiler)
            {
                t.Durum = SiparisTalepDurumu.Gonderildi;
                t.OnayTarihi = DateTime.Now;
                t.OnaylayanKullanici = kullanici;
                t.OnayNotu = $"Doğrulama yapılmadan kapatıldı — {gunEski} günden eski, önceden gönderilmiş sipariş.";
                t.GuncellemeTarihi = DateTime.Now;
            }

            if (eskiler.Count > 0) await _db.SaveChangesAsync(ct);
            return eskiler.Count;
        }

        // ═══════════════════════════════════════════════════════════
        //  TOPLU ONAY (AYNEN)
        // ═══════════════════════════════════════════════════════════
        public async Task<int> TopluOnaylaAynenAsync(
            IEnumerable<int> talepIds, string? kullanici, CancellationToken ct)
        {
            var idList = talepIds.Distinct().ToList();
            if (idList.Count == 0) return 0;

            await HesaplaVeYazAsync(idList, ct);

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
                t.OnayNotu = "Toplu onay — istenen miktarlarla";
                t.GuncellemeTarihi = DateTime.Now;
            }

            await _db.SaveChangesAsync(ct);
            return talepler.Count;
        }

        // ═══════════════════════════════════════════════════════════
        //  YARDIMCILAR
        // ═══════════════════════════════════════════════════════════

        private class TestKisa
        {
            public int TestID { get; set; }
            public string TestName { get; set; } = "";
            public TestGirisTipi GirisTipi { get; set; }
            public int? KutuBasinaAdet { get; set; }
        }

        private async Task<Dictionary<int, TestKisa>> TestleriGetirAsync(
            IEnumerable<SiparisTalepKalem> kalemler, CancellationToken ct)
        {
            var ids = kalemler.Where(k => k.TestID.HasValue).Select(k => k.TestID!.Value).Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, TestKisa>();

            var liste = await _db.Tests.AsNoTracking()
                .Where(x => ids.Contains(x.TestID))
                .Select(x => new TestKisa
                {
                    TestID = x.TestID,
                    TestName = x.TestName,
                    GirisTipi = x.GirisTipi,
                    KutuBasinaAdet = x.KutuBasinaAdet
                })
                .ToListAsync(ct);

            return liste.ToDictionary(x => x.TestID);
        }

        /// <summary>
        /// Kalemin gösterim birimi: 1 kutu kaç test.
        /// Ekran ve onay AYNI kuralı kullanır — ekranda "3 kutu" yazıp
        /// kaydederken başka çarpanla çevirmemek için tek yerde.
        /// </summary>
        private static (int Carpan, bool KutuBirimli) KalemCarpani(
            SiparisTalepKalem k, TestKisa? test, StokHesapSatirVm? satir)
        {
            // 1) Motorun kullandığı birim (katalog + sayım bilgisi)
            if (satir != null)
                return (Math.Max(1, satir.BoxTextCount), satir.KutuBirimli);

            // 2) Katalog
            if (test != null)
            {
                if (test.GirisTipi == TestGirisTipi.Kutu && test.KutuBasinaAdet.GetValueOrDefault() > 0)
                    return (test.KutuBasinaAdet!.Value, true);

                if (test.GirisTipi == TestGirisTipi.Adet)
                    return (1, false);
            }

            // 3) Sipariş çekilirken kullanılan oran
            if (k.HastaneTalepKutu.GetValueOrDefault() > 0
                && k.HastaneTalepTest > k.HastaneTalepKutu!.Value
                && k.HastaneTalepTest % k.HastaneTalepKutu.Value == 0)
            {
                return (k.HastaneTalepTest / k.HastaneTalepKutu.Value, true);
            }

            return (1, false);
        }

        /// <summary>"2,5" ve "2.5" ikisini de okur.</summary>
        private static decimal? OndalikOku(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var temiz = s.Trim().Replace(" ", "").Replace(',', '.');
            return decimal.TryParse(temiz, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                ? v
                : null;
        }

        private static string DurumAdi(SiparisTalepDurumu d) => d switch
        {
            SiparisTalepDurumu.Beklemede => "Bekliyor",
            SiparisTalepDurumu.OnaylandiAynen => "Onaylandı",
            SiparisTalepDurumu.OnaylandiDuzeltildi => "Onaylandı (düzeltildi)",
            SiparisTalepDurumu.Reddedildi => "Reddedildi",
            SiparisTalepDurumu.Gonderildi => "Gönderildi",
            _ => d.ToString()
        };
    }
}
