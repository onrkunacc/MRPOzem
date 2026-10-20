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
                Kategori = "Mapping",
                Baslik = "Reaktif ürün → Test eşleşmesi",
                Aciklama = "Fiziki sayımda görünen reaktif ürünlerin Tests tablosundaki SutCode ile eşleşip eşleşmediği kontrol edilir.",
                CozumOnerisi = "Bu reaktifler stok hesabına girmez. Tests tablosunda SutCode değerini kontrol et — leading-zero farkı normalize ediliyor ama farklı yazım varsa (örn. 'E124' vs '01E0124') eşleşme olmaz.",
                AksiyonUrl = "/Istatistik/Test/Index",
                AksiyonText = "Testleri Göster"
            };

            var eslesmeyenler = await _db.FizikiSayimSnapshotlari
                .AsNoTracking()
                .Where(s => s.SayimAktifMi
                         && s.UrunTipi == UrunTipi.Reaktif
                         && !s.MappedTestID.HasValue)
                .Select(s => new { s.ProductName, s.ReferenceNumber, s.HospitalID })
                .Take(100)
                .ToListAsync(ct);

            k.BulguAdeti = eslesmeyenler.Count;
            if (eslesmeyenler.Count == 0)
            {
                k.Durum = KontrolDurumu.Gecti;
            }
            else
            {
                k.Durum = eslesmeyenler.Count > 10 ? KontrolDurumu.Hata : KontrolDurumu.Dikkat;
                k.BulguDetaylar = eslesmeyenler
                    .Take(20)
                    .Select(x => $"{x.ProductName} · Ref: {x.ReferenceNumber ?? "—"} · Hastane #{x.HospitalID}")
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
                Aciklama = "Hesap stok (sayım + gönderim − kullanım) negatif çıkan kalemler. Kullanım, gönderimden fazla kayıt edilmiş demektir.",
                CozumOnerisi = "İlgili hastane için İstatistik girişlerini kontrol et — hastane sayımdan sonra fazla kullanım bildirilmiş olabilir, veya gönderim kaydı eksiktir.",
                AksiyonUrl = "/Lojistik/StokOneri/Index",
                AksiyonText = "Stok Öneri Ekranı"
            };

            var negatifler = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .Include(x => x.SiparisTalep)
                .ThenInclude(t => t!.Hospital)
                .Where(x => x.HesapGuncelStok < 0)
                .Select(x => new
                {
                    x.ProductName,
                    x.ReferenceNumber,
                    x.HesapGuncelStok,
                    HospitalName = x.SiparisTalep!.Hospital!.HospitalName
                })
                .Take(50)
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
                    .Select(x => $"{x.HospitalName} · {x.ProductName} · Stok: {x.HesapGuncelStok}")
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
                Aciklama = "Mapping'li hastaneler için son sayım tarihinin 120 günden eski olup olmadığı veya hiç sayım kaydı bulunmadığı kontrol edilir.",
                CozumOnerisi = "Eski sayım olan hastanelerde yeni sayım planla — stok hesabı güvenilir değil. Operasyon kuralı: her hastane 3 ayda bir sayılır."
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
                Baslik = "Onaylanmış ama kalem miktarı 0/null olanlar",
                Aciklama = "Durumu 'Onaylandı' olan taleplerde her kalemin OnaylananTest > 0 olması gerekir. 0 veya null kalanlar bug sinyali.",
                CozumOnerisi = "Doğrulama ekranında ilgili talebi tekrar aç, miktarları gözden geçir. Düzeltilmiş onay akışında bir sorun olabilir."
            };

            var hatalilar = await _db.SiparisTalepKalemleri
                .AsNoTracking()
                .Include(x => x.SiparisTalep)
                .ThenInclude(t => t!.Hospital)
                .Where(x => (x.SiparisTalep!.Durum == SiparisTalepDurumu.OnaylandiAynen
                          || x.SiparisTalep.Durum == SiparisTalepDurumu.OnaylandiDuzeltildi)
                         && (!x.OnaylananTest.HasValue || x.OnaylananTest == 0))
                .Select(x => new
                {
                    x.SiparisTalepID,
                    x.ProductName,
                    HospitalName = x.SiparisTalep!.Hospital!.HospitalName,
                    x.OnaylananTest
                })
                .Take(50)
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
                    .Select(x => $"Talep #{x.SiparisTalepID} · {x.HospitalName} · {x.ProductName} · OnaylananTest: {x.OnaylananTest?.ToString() ?? "null"}")
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
                Kategori = "Mapping",
                Baslik = "Ölü mapping — uzun süredir sipariş yok",
                Aciklama = "Aktif eşleştirilmiş olduğu halde 90 günden uzun süredir hiç sipariş gelmeyen müşteri kodları.",
                CozumOnerisi = "Bu müşteriler sipariş vermeyi bırakmış olabilir, veya musteri_kodu değişmiş olabilir. İlgili hastaneye sor; gerekiyorsa Yoksay işaretle.",
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
                Baslik = "Aşırı uç sapma — veri girişi hatası sinyali",
                Aciklama = "Bekleyen taleplerde SapmaYuzde > %300 (veya < -%95) olan kalemler. Normalde bu sapmalar fiziksel olarak anlamsız — veri hatası demek.",
                CozumOnerisi = "İlgili kalemde: ambalaj yanlış mı (adet/kutu karışmış mı), KutuBasinaAdet doğru mu, hastane yanlış sayı mı yazdı?"
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
                        $"istek: {x.HastaneTalepTest} / öneri: {x.OnerilenTest} / sapma: {x.SapmaYuzde:+0.0;-0.0;0}%")
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
                Baslik = "Hedef dönem anormal",
                Aciklama = "Bekleyen taleplerin HedefDonem değerinin bugünden 90 gün öncesi ile 90 gün sonrası dışında olması durumu.",
                CozumOnerisi = "PHP tarafında siparis_tarihi alanı yanlış olabilir, veya hastane geçmiş/gelecek döneme ait sipariş yazmış."
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
    }
}
