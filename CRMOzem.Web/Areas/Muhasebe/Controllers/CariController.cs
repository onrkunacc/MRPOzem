using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Muhasebe.Controllers
{
    [Area("Muhasebe")]
    [Authorize(Roles = "Yönetici,Muhasebe")]
    public class CariController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CariController(ApplicationDbContext context)
        {
            _context = context;
        }

        
        public async Task<IActionResult> Index(string search, int page = 1, int pageSize = 50)
        {
            var carilerQuery = _context.Carilers
                .Include(c => c.CariGrubu)
                .AsQueryable();

            // Filtreleme (Unvan, Cari Grubu, İl, TeklifNo, IhaleNo, CariKodu)
            if (!string.IsNullOrEmpty(search))
            {
                string lowerSearch = search.ToLower();

                carilerQuery = carilerQuery.Where(c =>
                    c.Unvan.ToLower().Contains(lowerSearch) ||
                    (c.CariGrubu != null && c.CariGrubu.GrupAdi.ToLower().Contains(lowerSearch)) ||
                    c.Il.ToLower().Contains(lowerSearch) ||
                    (!string.IsNullOrEmpty(c.TeklifNo) && c.TeklifNo.ToLower().Contains(lowerSearch)) || 
                    (!string.IsNullOrEmpty(c.IhaleNo) && c.IhaleNo.ToLower().Contains(lowerSearch)) ||   
                    (!string.IsNullOrEmpty(c.CariKodu) && c.CariKodu.ToLower().Contains(lowerSearch))    
                );
            }

            var totalRecords = await carilerQuery.CountAsync();

            var cariler = await carilerQuery
                .OrderBy(c => c.Unvan)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            ViewBag.SearchTerm = search;

            return View(cariler);
        }

        public IActionResult Create()
        {
            var cariGruplari = _context.CariGruplaris
                .Select(cg => new SelectListItem
                {
                    Value = cg.Id.ToString(),
                    Text = cg.GrupAdi
                }).ToList();

            var viewModel = new CariCreateViewModel
            {
                Cari = new Cariler(),
                CariGruplari = cariGruplari
            };

            return View(viewModel);
        }


     
        [HttpPost]
        public async Task<IActionResult> Create(CariCreateViewModel viewModel)
        {
            Console.WriteLine($"Gelen CariGrubuId: {viewModel.Cari.CariGrubuId}");

            ModelState.Remove("CariGruplari");
            ModelState.Remove("Cari.Invoices");
            ModelState.Remove("Cari.CariGrubu");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState Hataları:");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }

                viewModel.CariGruplari = _context.CariGruplaris
                    .Select(cg => new SelectListItem
                    {
                        Value = cg.Id.ToString(),
                        Text = cg.GrupAdi
                    }).ToList();

                return View(viewModel);
            }

            _context.Carilers.Add(viewModel.Cari);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cari başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var cari = await _context.Carilers
                    .Include(c => c.CariGrubu)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cari == null)
                {
                    TempData["ErrorMessage"] = "Cari bulunamadı!";
                    return RedirectToAction("Index");
                }

                var cariGruplari = await _context.CariGruplaris
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.GrupAdi
                    })
                    .ToListAsync();

                var viewModel = new CariEditViewModel
                {
                    Id = cari.Id,
                    CariKodu = cari.CariKodu,
                    Unvan = cari.Unvan,
                    Tevkifat = cari.Tevkifat,
                    Il = cari.Il,
                    Ilce = cari.Ilce,
                    CariGrubuId = cari.CariGrubuId,
                    CariGruplari = cariGruplari
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Bir hata oluştu: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CariEditViewModel viewModel)
        {
            try
            {
                ModelState.Remove("CariGruplari");

                if (!ModelState.IsValid)
                {

                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine($"ModelState Hatası: {error.ErrorMessage}");
                    }

                    viewModel.CariGruplari = await _context.CariGruplaris
                        .Select(g => new SelectListItem
                        {
                            Value = g.Id.ToString(),
                            Text = g.GrupAdi
                        })
                        .ToListAsync();

                    return View(viewModel);
                }

                var existingCari = await _context.Carilers.FindAsync(viewModel.Id);
                if (existingCari == null)
                {
                    TempData["ErrorMessage"] = "Cari bulunamadı.";
                    return RedirectToAction("Index");
                }

                existingCari.CariKodu = viewModel.CariKodu;
                existingCari.Unvan = viewModel.Unvan;
                existingCari.Tevkifat = viewModel.Tevkifat;
                existingCari.CariGrubuId = viewModel.CariGrubuId;
                existingCari.Il = viewModel.Il;
                existingCari.Ilce = viewModel.Ilce;

                _context.Carilers.Update(existingCari);  
                int affectedRows = await _context.SaveChangesAsync();

                if (affectedRows > 0)
                {
                    TempData["SuccessMessage"] = "Cari başarıyla güncellendi.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Güncelleme başarısız oldu. Veri değişmemiş olabilir.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Bir hata oluştu: " + ex.Message;
                return View(viewModel);
            }
        }



        [Area("Muhasebe")]
        [HttpGet]
        public async Task<IActionResult> Details(int id, int? year)
        {
            int currentYear = year ?? DateTime.Now.Year;

            // Cari ve faturaları getir
            var cari = await _context.Carilers
                .Include(c => c.CariGrubu)
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cari == null)
            {
                return NotFound("Cari bulunamadı.");
            }

            // Faturaları filtrele
            var invoicesForSelectedYear = cari.Invoices
                .Where(f => f.Donemi == currentYear)
                .ToList();

            // Toplam Fatura Tutarını Hesapla
            decimal toplamFaturaTutari = invoicesForSelectedYear.Sum(f => Convert.ToDecimal(f.Tutar));


            // 12 aylık listeyi oluştur ve faturalarla eşleştir
            var allMonths = Enumerable.Range(1, 12)
                .Select(month => new FaturaViewModel
                {
                    AyIndex = month,
                    AyAdi = new[]
                    {
                        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
                    }[month - 1],
                    Tutar = invoicesForSelectedYear.FirstOrDefault(f => f.Ay == month)?.Tutar,
                    FaturaVarMi = invoicesForSelectedYear.Any(f => f.Ay == month),
                    FaturaId = invoicesForSelectedYear.FirstOrDefault(f => f.Ay == month)?.Id,
                    CariId = id
                }).ToList();

            // ViewModel'e Toplam Tutarı Ekleyelim
            var viewModel = new
            {
                Cari = cari,
                Faturalar = allMonths,
                CurrentYear = currentYear,
                AvailableYears = _context.Invoices
                    .Where(i => i.CariId == id)
                    .Select(i => i.Donemi)
                    .Distinct()
                    .ToList(),
                ToplamFaturaTutari = toplamFaturaTutari // ✅ Toplamı View'e gönderiyoruz
            };

            return View(viewModel);
        }

        // Cari Silme
        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            var cari = await _context.Carilers.FindAsync(id);
            if (cari != null)
            {
                _context.Carilers.Remove(cari);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cari başarıyla silindi!" });
            }
            return Json(new { success = false, message = "Cari bulunamadı!" });
        }
        [HttpPost]
        public async Task<IActionResult> UpdateOdemeYazisi([FromBody] UpdateOdemeYazisiViewModel model)
        {
            if (model == null || model.Id <= 0 || string.IsNullOrEmpty(model.Link))
            {
                return BadRequest("Geçersiz veri.");
            }

            var cari = await _context.Carilers.FindAsync(model.Id);
            if (cari == null)
            {
                return NotFound("Cari bulunamadı.");
            }

            cari.OdemeYazisiLinki = model.Link;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Ödeme yazısı başarıyla güncellendi." });
        }
    }
}
