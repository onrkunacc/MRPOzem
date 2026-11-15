using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Enums;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class SaglikRaporuService : ISaglikRaporuService
    {
        private readonly ApplicationDbContext _db;

        public SaglikRaporuService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<SaglikRaporuVm> UretAsync(CancellationToken ct)
        {
            var vm = new SaglikRaporuVm();

            vm.Kontroller.Add(await K01_EslesmeyenReaktifler(ct));
            vm.Kontroller.Add(await K02_NegatifStok(ct));
            vm.Kontroller.Add(await K03_SayimEskiVeyaYok(ct));
            vm.Kontroller.Add(await K04_OnayliAmaEksikKalemler(ct));
            vm.Kontroller.Add(await K05_OluMappingler(ct));
            vm.Kontroller.Add(await K06_UcSapmalar(ct));
            vm.Kontroller.Add(await K07_DonemUyumsuzlugu(ct));
            vm.Kontroller.Add(await K08_KullanimGirisiGeride(ct));

            return vm;
        }

        // ═══════════════════════════════════════════════════════════
        //  K01 — Snapshot'ta Reaktif var ama Tests tablosuyla eşleşmedi
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K01_EslesmeyenReaktifler(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K01",
                Kategori = "Eşleştirme",
                Baslik = "Sayımda olup katalogda bulunmayan reaktifler",
                Aciklama = "Sayımda görünen reaktif ürünün referans numarası Testler listesinde yoksa o ürünün sayımı stok hesabına girmez.",
                CozumOnerisi = "Testler listesinde ürünün referans (SUT) kodunu kontrol edin. Tire, nokta, boşluk ve baştaki sıfır farkları artık sorun değil; kodun kendisi farklıysa ya da ürün listede hiç yoksa eklenmeli. Düzelttikten sonra Dashboard'dan \"Sayımları Yenile\" deyin.",
                AksiyonUrl = "/Istatistik/Test/Index",
                AksiyonText = "Testler listesi"
            };

            var ham = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => s.SayimAktifMi
                         && s.UrunTipi == UrunTipi.Reaktif
                         && !s.MappedTestID.HasValue)
                .Select(s => new { s.ProductName, s.ReferenceNumber, s.HospitalID })
                .ToListAsync(ct);

            // Aynı ürün her hastanede ve her lotta ayrı satırdır — ürün bazında topla
            var urunler = ham
                .GroupBy(x => (x.ReferenceNumber ?? "").Trim().ToUpperInvariant())
                .Select(g => new
                {
                    Ref = g.Key,
                    Ad = g.First().ProductName,
                    Hastane = g.Select(x => x.HospitalID).Distinct().Count()
                })
                .OrderByDescending(x => x.Hastane)
                .ToList();

            k.BulguAdeti = urunler.Count;
            if (urunler.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = urunler.Count > 10 ? KontrolDurumu.Hata : KontrolDurumu.Dikkat;
                k.BulguDetaylar = urunler
                    .Take(20)
                    .Select(x => $"{x.Ad} · Ref: {(x.Ref.Length > 0 ? x.Ref : "—")} · {x.Hastane} hastanede")
                    .ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K02 — Hesap stok negatif
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K02_NegatifStok(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K02",
                Kategori = "Hesaplama",
                Baslik = "Negatif güncel stok",
                Aciklama = "Bekleyen siparişlerde hesaplanan stok (sayım + gönderim − kullanım) eksi çıkan kalemler. Kâğıt üzerinde olmayan ürün kullanılmış görünüyor.",
                CozumOnerisi = "Hastanenin gönderim kayıtlarını ve İstatistik (kullanım) girişlerini karşılaştırın: ya bir gönderim kaydı eksik ya da kullanım fazla girilmiş. Raporlama ekranında hastanenin gönderim–kullanım dengesine bakabilirsiniz.",
                AksiyonUrl = "/Lojistik/Rapor",
                AksiyonText = "Raporlama"
            };

            var negatifler = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .Include(x => x.SiparisTalep)
                .ThenInclude(t => t!.Hospital)
                .Where(x => x.HesapGuncelStok < 0
                         && x.SiparisTalep!.Durum == SiparisTalepDurumu.Beklemede)
                .Select(x => new
                {
                    x.ProductName,
                    x.ReferenceNumber,
                    x.HesapGuncelStok,
                    HospitalName = x.SiparisTalep!.Hospital!.HospitalName
                })
                .ToListAsync(ct);

            k.BulguAdeti = negatifler.Count;
            if (negatifler.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = KontrolDurumu.Hata;
                k.BulguDetaylar = negatifler
                    .Take(20)
                    .Select(x => $"{x.HospitalName} · {x.ProductName} · hesaplanan stok: {x.HesapGuncelStok:N0} test")
                    .ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K03 — Sayım eski veya hiç yok
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K03_SayimEskiVeyaYok(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K03",
                Kategori = "Veri bütünlüğü",
                Baslik = "Fiziki sayım güncelliği",
                Aciklama = "Sipariş veren hastanelerde son sayım 120 günden eski mi, ya da hiç sayım kaydı yok mu?",
                CozumOnerisi = "Sayımı eski hastanelerde yeni sayım planlayın (kural: 3 ayda bir). \"Hiç sayım kaydı yok\" görünen hastanede sayım yapıldıysa sorun eşleşmededir: aşağıdaki \"Düşmeyen sayımlar\" bölümüne bakın.",
                AksiyonUrl = "/Lojistik/Dashboard/SayimEslesme",
                AksiyonText = "Sayım eşleşmesi"
            };

            var sinir = DateTime.Now.AddDays(-120);

            var aktifMap = await _db.HospitalEslesmeleri
                .AsNoTracking()
                .Where(e => !e.Yoksay && e.InternalHospitalID.HasValue)
                .Select(e => new { HospitalID = e.InternalHospitalID!.Value })
                .Distinct()
                .ToListAsync(ct);

            var hospitalIds = aktifMap.Select(x => x.HospitalID).ToList();

            var sonSayim = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => hospitalIds.Contains(s.HospitalID) && s.SayimAktifMi)
                .GroupBy(s => s.HospitalID)
                .Select(g => new { HospitalID = g.Key, MaxTarih = g.Max(s => s.SayimTarihi) })
                .ToListAsync(ct);

            var sayimYapilan = sonSayim.Select(s => s.HospitalID).ToHashSet();

            var hastaneAdlari = await _db.Hospitals
                .AsNoTracking()
                .Where(h => hospitalIds.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);
            var adMap = hastaneAdlari.ToDictionary(x => x.HospitalID, x => x.HospitalName);

            var bulgular = new List<string>();

            // Sayım hiç yok
            foreach (var id in hospitalIds.Where(id => !sayimYapilan.Contains(id)))
            {
                bulgular.Add($"{adMap.GetValueOrDefault(id, "—")} · hiç sayım kaydı yok");
            }

            // Sayım eski
            foreach (var s in sonSayim.Where(x => x.MaxTarih < sinir))
            {
                var gun = (int)(DateTime.Now - s.MaxTarih).TotalDays;
                bulgular.Add($"{adMap.GetValueOrDefault(s.HospitalID, "—")} · son sayım {gun} gün önce ({s.MaxTarih:dd.MM.yyyy})");
            }

            k.BulguAdeti = bulgular.Count;
            if (bulgular.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = bulgular.Count > 3 ? KontrolDurumu.Hata : KontrolDurumu.Dikkat;
                k.BulguDetaylar = bulgular.Take(20).ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K04 — Onaylanmış talepte OnaylananTest NULL/0 kalemler
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K04_OnayliAmaEksikKalemler(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K04",
                Kategori = "Veri bütünlüğü",
                Baslik = "Onaylanmış ama miktarı kaydedilmemiş kalemler",
                Aciklama = "Onaylanan siparişte her kalemin onay miktarı kayıtlı olmalı. Miktarı bilerek 0 yapılan (gönderilmeyecek) kalemler sorun sayılmaz; burada yalnızca miktarı hiç yazılmamış olanlar listelenir.",
                CozumOnerisi = "Bu kayıtlar eski sürümde onaylanmış olabilir. Sayıları azsa yok sayılabilir; \"yolda\" hesabına girmezler."
            };

            var hatalilar = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .Include(x => x.SiparisTalep)
                .ThenInclude(t => t!.Hospital)
                .Where(x => (x.SiparisTalep!.Durum == SiparisTalepDurumu.OnaylandiAynen
                          || x.SiparisTalep.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi)
                         && !x.OnaylananTest.HasValue)
                .Select(x => new
                {
                    x.SiparisTalepID,
                    x.ProductName,
                    HospitalName = x.SiparisTalep!.Hospital!.HospitalName,
                    x.OnaylananTest
                })
                .ToListAsync(ct);

            k.BulguAdeti = hatalilar.Count;
            if (hatalilar.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = KontrolDurumu.Dikkat;
                k.BulguDetaylar = hatalilar
                    .Take(20)
                    .Select(x => $"Talep #{x.SiparisTalepID} · {x.HospitalName} · {x.ProductName}")
                    .ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K05 — Ölü mapping: Aktif eşleşme ama 90+ gündür sipariş gelmemiş
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K05_OluMappingler(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K05",
                Kategori = "Eşleştirme",
                Baslik = "Uzun süredir sipariş gelmeyen hastaneler",
                Aciklama = "Sipariş yazılımıyla eşleştirilmiş ama 90 günden uzun süredir siparişi görünmeyen müşteri kodları.",
                CozumOnerisi = "Hastane sipariş vermeyi bırakmış ya da sipariş yazılımındaki müşteri kodu değişmiş olabilir. Kod değiştiyse yeni kodu Hastane Eşleşmesi ekranında bağlayın; aşağıdaki \"Gelmeyen siparişler\" bölümü yeni kodu gösterir.",
                AksiyonUrl = "/Lojistik/HospitalEslesmesi/Index",
                AksiyonText = "Eşleştirmeyi Düzenle"
            };

            var sinir = DateTime.Now.AddDays(-90);

            var aktifEslesmeler = await _db.HospitalEslesmeleri
                .AsNoTracking()
                .Include(e => e.InternalHospital)
                .Where(e => !e.Yoksay && e.InternalHospitalID.HasValue)
                .ToListAsync(ct);

            // Her mapping için son sipariş tarihi
            var hospitalIds = aktifEslesmeler.Select(e => e.InternalHospitalID!.Value).Distinct().ToList();

            var sonSiparisler = await _db.SiparisTalepler
                .AsNoTracking()
                .Where(t => hospitalIds.Contains(t.HospitalID))
                .GroupBy(t => t.HospitalID)
                .Select(g => new { HospitalID = g.Key, SonTarih = g.Max(t => t.TalepTarihi) })
                .ToListAsync(ct);
            var sonMap = sonSiparisler.ToDictionary(x => x.HospitalID, x => x.SonTarih);

            var oluler = aktifEslesmeler
                .Where(e =>
                {
                    var hospitalId = e.InternalHospitalID!.Value;
                    if (!sonMap.TryGetValue(hospitalId, out var sonTarih))
                        return true;  // hiç sipariş yok
                    return sonTarih < sinir;
                })
                .Select(e =>
                {
                    var hospitalId = e.InternalHospitalID!.Value;
                    var sonTarih = sonMap.TryGetValue(hospitalId, out var dt) ? dt : (DateTime?)null;
                    var hastaneAdi = e.InternalHospital?.HospitalName ?? "—";
                    var gun = sonTarih.HasValue ? ((int)(DateTime.Now - sonTarih.Value).TotalDays).ToString() + " gün önce" : "hiç";
                    return $"{e.ExternalMusteriKodu} ({hastaneAdi}) · son sipariş: {gun}";
                })
                .ToList();

            k.BulguAdeti = oluler.Count;
            if (oluler.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = KontrolDurumu.Dikkat;
                k.BulguDetaylar = oluler.Take(20).ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K06 — Uç sapmalar (HastaneTalepTest çok büyük veya çok küçük)
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K06_UcSapmalar(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K06",
                Kategori = "Hesaplama",
                Baslik = "Öneriden çok uzak istekler",
                Aciklama = "Bekleyen siparişlerde istenen miktar önerinin 4 katından fazla (ya da neredeyse hiç) olan kalemler. Çoğu zaman gerçek ihtiyaç değil, birim ya da veri hatasıdır.",
                CozumOnerisi = "Kalemde şunlara bakın: kutu yerine adet (ya da tersi) mi yazılmış, Testler listesindeki \"kutu başına adet\" doğru mu, hastane yanlış sayı mı girmiş?",
                AksiyonUrl = "/Lojistik/Dogrulama",
                AksiyonText = "Doğrulama"
            };

            var sapanlar = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .Include(x => x.SiparisTalep)
                .ThenInclude(t => t!.Hospital)
                .Where(x => x.SiparisTalep!.Durum == SiparisTalepDurumu.Beklemede
                         && x.SapmaYuzde.HasValue
                         && (x.SapmaYuzde > 300m || x.SapmaYuzde < -95m))
                .Select(x => new
                {
                    x.SiparisTalepID,
                    x.ProductName,
                    HospitalName = x.SiparisTalep!.Hospital!.HospitalName,
                    x.HastaneTalepTest,
                    x.OnerilenTest,
                    x.SapmaYuzde
                })
                .Take(50)
                .ToListAsync(ct);

            k.BulguAdeti = sapanlar.Count;
            if (sapanlar.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = KontrolDurumu.Dikkat;
                k.BulguDetaylar = sapanlar
                    .Take(20)
                    .Select(x =>
                        $"Talep #{x.SiparisTalepID} · {x.HospitalName} · {x.ProductName} · " +
                        $"istenen: {x.HastaneTalepTest:N0} test · önerilen: {x.OnerilenTest:N0} test · fark: %{x.SapmaYuzde:+0;-0;0}")
                    .ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K07 — Dönem uyumsuzluğu (çok eski veya çok ileri)
        // ═══════════════════════════════════════════════════════════
        private async Task<SaglikKontrolVm> K07_DonemUyumsuzlugu(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K07",
                Kategori = "Veri bütünlüğü",
                Baslik = "Tarihi tuhaf siparişler",
                Aciklama = "Bekleyen siparişlerden tarihi bugünden 90 günden fazla geride ya da ileride olanlar.",
                CozumOnerisi = "Sipariş yazılımındaki sipariş tarihi yanlış olabilir. Sipariş geçerli değilse Doğrulama ekranından reddedin."
            };

            var ustSinir = DateTime.Now.AddDays(90);
            var altSinir = DateTime.Now.AddDays(-90);

            var anormaller = await _db.SiparisTalepler
                .AsNoTracking()
                .Include(t => t.Hospital)
                .Where(t => t.Durum == SiparisTalepDurumu.Beklemede
                         && t.HedefDonem.HasValue
                         && (t.HedefDonem < altSinir || t.HedefDonem > ustSinir))
                .Select(t => new
                {
                    t.ID,
                    HospitalName = t.Hospital!.HospitalName,
                    t.HedefDonem,
                    t.TalepTarihi
                })
                .Take(50)
                .ToListAsync(ct);

            k.BulguAdeti = anormaller.Count;
            if (anormaller.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = KontrolDurumu.Dikkat;
                k.BulguDetaylar = anormaller
                    .Take(20)
                    .Select(x => $"Talep #{x.ID} · {x.HospitalName} · dönem: {x.HedefDonem:MMMM yyyy} · talep tarihi: {x.TalepTarihi:dd.MM.yyyy}")
                    .ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }

        // ═══════════════════════════════════════════════════════════
        //  K08 — Kullanım (İstatistik) girişi geride kalan hastaneler
        // ═══════════════════════════════════════════════════════════
        //  Stok hesabındaki "kullanım" İstatistik modülündeki aylık test
        //  sayılarından (MonthlyStatistics) gelir. Giriş yapılmayan aylar
        //  ortalamadan TAHMİN edilir; giriş ne kadar gerideyse tahmin payı
        //  o kadar büyür.
        private async Task<SaglikKontrolVm> K08_KullanimGirisiGeride(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var k = new SaglikKontrolVm
            {
                Kod = "K08",
                Kategori = "Veri bütünlüğü",
                Baslik = "Kullanım (istatistik) girişi geride kalan hastaneler",
                Aciklama = "Stok hesabındaki kullanım, İstatistik modülüne girilen aylık test sayılarından alınır. Geçen ayın girişi yapılmamış hastanelerde aradaki dönem ortalamadan tahmin edilir.",
                CozumOnerisi = "Bu hastanelerin eksik aylarını İstatistik modülünden girin. Giriş tamamlanınca stok tahmini kesinleşir ve veri güveni yükselir.",
                AksiyonUrl = "/Lojistik/Rapor",
                AksiyonText = "Raporlama"
            };

            var buAy = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var gecenAy = buAy.AddMonths(-1);

            var hospitalIds = await _db.HospitalEslesmeleri
                .AsNoTracking()
                .Where(e => !e.Yoksay && e.InternalHospitalID.HasValue)
                .Select(e => e.InternalHospitalID!.Value)
                .Distinct()
                .ToListAsync(ct);

            var sonAylar = await _db.MonthlyStatistics
                .AsNoTracking()
                .Where(m => hospitalIds.Contains(m.HospitalID))
                .GroupBy(m => m.HospitalID)
                .Select(g => new { HospitalID = g.Key, Son = g.Max(x => x.Month) })
                .ToListAsync(ct);
            var sonMap = sonAylar.ToDictionary(x => x.HospitalID, x => x.Son);

            var adlar = await _db.Hospitals
                .AsNoTracking()
                .Where(h => hospitalIds.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.HospitalName })
                .ToListAsync(ct);

            var bulgular = new List<(int Sira, string Metin)>();
            foreach (var h in adlar)
            {
                if (!sonMap.TryGetValue(h.HospitalID, out var son))
                {
                    bulgular.Add((999, $"{h.HospitalName} · hiç kullanım girişi yok"));
                    continue;
                }

                var sonAy = new DateTime(son.Year, son.Month, 1);
                if (sonAy >= gecenAy) continue;

                var eksik = ((buAy.Year - sonAy.Year) * 12 + buAy.Month - sonAy.Month) - 1;
                bulgular.Add((eksik, $"{h.HospitalName} · son giriş {sonAy:MMMM yyyy} · {eksik} ay eksik"));
            }

            k.BulguAdeti = bulgular.Count;
            if (bulgular.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = KontrolDurumu.Dikkat;
                k.BulguDetaylar = bulgular
                    .OrderByDescending(x => x.Sira)
                    .Take(20)
                    .Select(x => x.Metin)
                    .ToList();
            }

            k.SureMs = sw.ElapsedMilliseconds;
            return k;
        }
    }
}
