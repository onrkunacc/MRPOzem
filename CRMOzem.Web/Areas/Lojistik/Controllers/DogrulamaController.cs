using CRMOzem.Business.Interfaces;
using CRMOzem.Core.ViewModels.Dogrulama;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Lojistik.Controllers
{
    [Area("Lojistik")]
    [Authorize(Roles = "Yönetici,KotaTakip,Lojistik")]
    public class DogrulamaController : Controller
    {
        private readonly IDogrulamaService _service;
        private readonly ISiparisApiClient _siparisApi;
        private readonly ApplicationDbContext _db;

        public DogrulamaController(IDogrulamaService service,ISiparisApiClient siparisApi,ApplicationDbContext db)
        {
            _service = service;
            _siparisApi = siparisApi;
            _db = db;   
        }

        // ═══════════════════════════════════════════════════════════
        //  INDEX — bekleyen talepler + filtre
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(DogrulamaFiltreVm? filtre, CancellationToken ct)
        {
            filtre ??= new DogrulamaFiltreVm();
            var vm = await _service.GetListAsync(filtre, ct);
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  DETAY — talep × kalem bazlı karşılaştırma
        // ═══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Detay(int id, CancellationToken ct)
        {
            var vm = await _service.GetDetayAsync(id, ct);
            if (vm == null)
            {
                TempData["Error"] = $"Talep #{id} bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════
        //  ONAYLA (AYNEN) — tek talep
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnaylaAynen(int id, string? onayNotu, CancellationToken ct)
        {
            var (ok, msg) = await _service.OnaylaAynenAsync(id, onayNotu, User.Identity?.Name, ct);
            if (ok) TempData["SuccessMessage"] = msg;
            else TempData["Error"] = msg;
            return RedirectToAction(nameof(Detay), new { id });
        }

        // ═══════════════════════════════════════════════════════════
        //  ONAYLA (DÜZELTİLMİŞ) — tek talep, kalem bazlı
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnaylaDuzeltilmis(OnayPostDto dto, CancellationToken ct)
        {
            var (ok, msg) = await _service.OnaylaDuzeltilmisAsync(dto, User.Identity?.Name, ct);
            if (ok) TempData["SuccessMessage"] = msg;
            else TempData["Error"] = msg;
            return RedirectToAction(nameof(Detay), new { id = dto.TalepID });
        }

        // ═══════════════════════════════════════════════════════════
        //  REDDET — tek talep
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string? redNedeni, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(redNedeni))
            {
                TempData["Error"] = "Red nedeni zorunlu.";
                return RedirectToAction(nameof(Detay), new { id });
            }
            var (ok, msg) = await _service.ReddetAsync(id, redNedeni, User.Identity?.Name, ct);
            if (ok) TempData["SuccessMessage"] = msg;
            else TempData["Error"] = msg;
            return RedirectToAction(nameof(Detay), new { id });
        }

        // ═══════════════════════════════════════════════════════════
        //  TOPLU ONAY (AYNEN) — birden çok talep
        // ═══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopluOnaylaAynen(int[] talepIds, CancellationToken ct)
        {
            if (talepIds == null || talepIds.Length == 0)
            {
                TempData["Error"] = "Hiç talep seçilmedi.";
                return RedirectToAction(nameof(Index));
            }
            var adet = await _service.TopluOnaylaAynenAsync(talepIds, User.Identity?.Name, ct);
            TempData["SuccessMessage"] = $"{adet} talep aynen onaylandı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> TestSyncSiparis(
           [FromServices] ISiparisSyncService sync,
           CancellationToken ct)
        {
            var sonuc = await sync.SyncAsync(ct);
            return Json(sonuc);
        }



        [HttpGet]
        public async Task<IActionResult> DebugApiOrnekler(CancellationToken ct)
        {
            // Sync ile BİREBİR aynı çağrı — limit 500
            var cevap = await _siparisApi.GetOrdersAsync(status: 1, sinceId: 0, limit: 500, ct: ct);

            if (cevap == null || cevap.Orders == null)
            {
                return Json(new { hata = "API cevap vermedi veya Orders null" });
            }

            var orders = cevap.Orders;

            // Boş/dolu sayıları
            int bosKod = orders.Count(o => string.IsNullOrWhiteSpace(o.CustomerCode));
            int doluKod = orders.Count - bosKod;

            // Tekil dolu kodlar
            var tekilDoluKodlar = orders
                .Where(o => !string.IsNullOrWhiteSpace(o.CustomerCode))
                .Select(o => o.CustomerCode!.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Boş CustomerCode olanlardan 10 örnek — hangi orderlar bunlar?
            var bosKodOrnekleri = orders
                .Where(o => string.IsNullOrWhiteSpace(o.CustomerCode))
                .OrderByDescending(o => o.OrderId)
                .Take(10)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    CustomerCode = o.CustomerCode ?? "(NULL)",
                    CustomerName = o.CustomerName ?? "(NULL)",
                    HospitalName = o.HospitalName ?? "(NULL)",
                    ItemsCount = o.Items?.Count ?? 0
                })
                .ToList();

            // En eski ve en yeni orderId
            int enEski = orders.Min(o => o.OrderId);
            int enYeni = orders.Max(o => o.OrderId);

            return Json(new
            {
                ToplamApiCevap = orders.Count,
                EnEskiOrderId = enEski,
                EnYeniOrderId = enYeni,

                BosCustomerCodeSayisi = bosKod,
                DoluCustomerCodeSayisi = doluKod,

                TekilDoluKodSayisi = tekilDoluKodlar.Count,
                TekilDoluKodlarIlk30 = tekilDoluKodlar.Take(30).ToList(),

                BosKodOrnekleri = bosKodOrnekleri
            });
        }


        [HttpGet]
        public async Task<IActionResult> DebugEslesme(CancellationToken ct)
        {
            // 1) API cevabı — sync ile aynı parametreler
            var cevap = await _siparisApi.GetOrdersAsync(status: 1, sinceId: 0, limit: 500, ct: ct);
            if (cevap?.Orders == null) return Json(new { hata = "API null" });

            // 2) Mapping tablosu — DB'de gerçekte ne var
            var tumEslesmeler = await _db.HospitalEslesmeleri.AsNoTracking().ToListAsync(ct);
            var eslesmeMap = tumEslesmeler
                .GroupBy(e => (e.ExternalMusteriKodu ?? "").Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            // 3) Zaten var olan ref'ler
            var mevcutRefler = await _db.SiparisTalepler
                .AsNoTracking()
                .Where(s => s.ExternalRefNo != null)
                .Select(s => s.ExternalRefNo!)
                .ToListAsync(ct);
            var mevcutRefSet = new HashSet<string>(mevcutRefler, StringComparer.OrdinalIgnoreCase);

            // 4) Sync mantığını birebir çalıştır
            int zatenVar = 0, bosKod = 0, eslesenKalemli = 0, eslesenKalemsiz = 0, yoksay = 0, eslesmeyen = 0;
            var eslesmeyenDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var o in cevap.Orders.OrderBy(x => x.OrderId))
            {
                var externalRef = o.OrderId.ToString();
                if (mevcutRefSet.Contains(externalRef)) { zatenVar++; continue; }

                var kod = (o.CustomerCode ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(kod)) { bosKod++; continue; }

                if (eslesmeMap.TryGetValue(kod, out var e))
                {
                    if (e.Yoksay || !e.InternalHospitalID.HasValue) yoksay++;
                    else if (o.Items == null || o.Items.Count == 0) eslesenKalemsiz++;
                    else eslesenKalemli++;
                    continue;
                }

                // Burası eslesmeyenToplam'ın karşılığı
                eslesmeyen++;
                if (!eslesmeyenDict.ContainsKey(kod)) eslesmeyenDict[kod] = 0;
                eslesmeyenDict[kod]++;
            }

            return Json(new
            {
                ApiToplam = cevap.Orders.Count,

                // DB state
                MappingTablosundaKayitSayisi = tumEslesmeler.Count,
                MappingTekilKodSayisi = eslesmeMap.Count,
                MevcutSiparisTalepSayisi = mevcutRefler.Count,

                // Sync akışı
                ZatenVar = zatenVar,
                BosKod = bosKod,
                EslesenYeniKalemli = eslesenKalemli,
                EslesenYeniKalemsiz = eslesenKalemsiz,
                YoksaySayisi = yoksay,
                EslesmeyenToplamSayac = eslesmeyen,
                EslesmeyenTekilSayi = eslesmeyenDict.Count,
                EslesmeyenKodlarVeAdetleri = eslesmeyenDict
                    .OrderByDescending(x => x.Value)
                    .ToDictionary(x => x.Key, x => x.Value),

                // Mapping tablosunun tüm içeriği — DB'de GERÇEKTE ne var?
                TumMappingler = tumEslesmeler.Select(m => new {
                    m.ExternalMusteriKodu,
                    m.Yoksay,
                    m.InternalHospitalID
                }).ToList()
            });
        }
    }
}
