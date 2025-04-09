using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using CRMOzem.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Web.Areas.Muhasebe.Controllers
{
    [Area("Muhasebe")]
    [Authorize(Roles = "Muhasebe,Yönetici")]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditReportService _auditReportService;
        private readonly ILogger<InvoiceController> _logger;
        public InvoiceController(ApplicationDbContext context,IAuditReportService auditReportService, ILogger<InvoiceController> logger)
        {
            _context = context;
            _auditReportService = auditReportService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string search, int page = 1, int pageSize = 25, int year = 0)
        {
            var invoicesQuery = _context.Invoices
                .Include(i => i.Cari)
                .OrderByDescending(i => i.Id)
                .AsQueryable();

            // Yıla göre filtreleme
            if (year > 0)
            {
                invoicesQuery = invoicesQuery.Where(i => i.Donemi == year);
            }

            // Faturaları önce bir listeye çekelim
            var invoicesList = await invoicesQuery.ToListAsync();

            // Ay isimlerini dönüştürelim
            var invoicesWithMonthNames = invoicesList
                .Select(i => new
                {
                    Invoice = i,
                    MonthName = CRMOzem.Business.Extensions.DateHelper.GetMonthName(i.Ay) // Ay ismini al
                })
                .ToList();

            // Arama (Cari Adı, Ay Adı, Tutar)
            if (!string.IsNullOrEmpty(search))
            {
                invoicesWithMonthNames = invoicesWithMonthNames
                    .Where(i =>
                        i.Invoice.Cari.Unvan.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        i.MonthName.Contains(search, StringComparison.OrdinalIgnoreCase) || // Ay adına göre filtreleme
                        i.Invoice.Tutar.ToString().Contains(search))
                    .ToList();
            }

            // Sayfalama işlemi
            var totalRecords = invoicesWithMonthNames.Count();
            var paginatedInvoices = invoicesWithMonthNames
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => i.Invoice)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            ViewBag.SearchTerm = search;
            ViewBag.SelectedYear = year;

            return View(paginatedInvoices);
        }
        public async Task<IActionResult> AddInvoice(int? cariId)
        {
            ViewBag.Cariler = await _context.Carilers
                .Select(c => new { c.Id, c.Unvan })
                .ToListAsync();

            ViewBag.SelectedCariId = cariId;
            return View(new Invoices { CariId = cariId ?? 0 });
        }

        [HttpPost]
        public async Task<IActionResult> AddInvoice(Invoices invoice)
        {
            try
            {
                // Tutarı tr-TR formatına göre parse et
                var cultureInfo = new System.Globalization.CultureInfo("tr-TR");
                invoice.Tutar = decimal.Parse(invoice.Tutar.ToString(cultureInfo), cultureInfo);
                ModelState.Remove(nameof(invoice.Cari));

                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.CariId == invoice.CariId && i.Ay == invoice.Ay && i.Donemi == invoice.Donemi);

                if (existingInvoice != null)
                {
                    TempData["ErrorMessage"] = $"Seçilen ayda ({invoice.Ay}/{invoice.Donemi}) bu cariye ait zaten bir fatura var. " +
                                                $"Fatura No: {existingInvoice.Id}, Tutar: {existingInvoice.Tutar.ToString("N2", cultureInfo)}.";

                    // Cariler tekrar yüklenir
                    ViewBag.Cariler = await _context.Carilers
                        .Select(c => new { c.Id, c.Unvan })
                        .ToListAsync();

                    return View(invoice);
                }

                // Validasyon kontrolü
                if (ModelState.IsValid)
                {
                    invoice.Ekler = string.IsNullOrWhiteSpace(invoice.Ekler) ? null : invoice.Ekler;

                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Fatura başarıyla eklendi.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
            }

            // Hata durumunda cariler tekrar yüklenir
            ViewBag.Cariler = await _context.Carilers
                .Select(c => new { c.Id, c.Unvan })
                .ToListAsync();

            return View(invoice);
        }

        // Fatura Silme
        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    return Json(new { success = false, message = "Fatura bulunamadı." });
                }

                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Fatura başarıyla silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Fatura silme hatası: {ex.Message}");
                return Json(new { success = false, message = "Fatura silinirken hata oluştu." });
            }
        }


        [HttpGet]
        public async Task<IActionResult> EditInvoice(int cariId, int month, int year)
        {
            try
            {
                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.CariId == cariId && i.Ay == month && i.Donemi == year);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Fatura bulunamadı.";
                    return RedirectToAction("Index");
                }

                var cari = await _context.Carilers
                    .FirstOrDefaultAsync(c => c.Id == cariId);

                if (cari == null)
                {
                    TempData["ErrorMessage"] = "Cari bulunamadı.";
                    return RedirectToAction("Index");
                }

                var viewModel = new EditInvoiceViewModel
                {
                    FaturaId = invoice.Id,
                    CariId = cari.Id,
                    CariAdi = cari.Unvan,
                    Ay = invoice.Ay,
                    Yil = invoice.Donemi,
                    Tutar = invoice.Tutar,
                    Ekler = invoice.Ekler
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        public async Task<IActionResult> EditInvoice(EditInvoiceViewModel model)
        {
            try
            {
               
                ModelState.Remove("CariAdi");

                if (ModelState.IsValid)
                {
                    var invoice = await _context.Invoices.FindAsync(model.FaturaId);

                    if (invoice == null)
                    {
                        TempData["ErrorMessage"] = "Fatura bulunamadı.";
                        return RedirectToAction("Index");
                    }
                    Console.WriteLine($"Model Tutar: {model.Tutar}");
                    Console.WriteLine($"Invoice Tutar (before update): {invoice.Tutar}");

                    var cultureInfo = new System.Globalization.CultureInfo("tr-TR");
                    invoice.Tutar = decimal.Parse(model.Tutar.ToString(cultureInfo), cultureInfo);
                    Console.WriteLine($"Invoice Tutar (after update): {invoice.Tutar}");
                    // Ekler alanı için boşluk kontrolü
                    invoice.Ekler = string.IsNullOrWhiteSpace(model.Ekler) ? null : model.Ekler;

                    // Veriyi güncelle ve kaydet
                    _context.Invoices.Update(invoice);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Fatura başarıyla güncellendi.";
                    return RedirectToAction("Details", "Cari", new { id = model.CariId, year = model.Yil });
                }
                else
                {
                    TempData["ErrorMessage"] = "Geçersiz form girişleri.";
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Bir hata oluştu: {ex.Message}");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> FilterInvoice(int year)
        {
            var invoices = await _context.Invoices
                .Include(f => f.Cari)
                .Where(f => f.Donemi == year)
                .ToListAsync();

            ViewBag.SelectedYear = year;
            return View("Index", invoices);
        }
        [HttpGet]
        public async Task<IActionResult> ExportToExcelView()
        {
            ViewBag.ErrorMessage = TempData["Error"];

            // ✅ Cari Grupları Listeyi Veritabanından Getir
            ViewBag.CariGruplari = await _context.CariGruplaris.ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel(int? cariGrupId, List<int>? cariIds, string? il, string? ihaleNo, int? donem, int? baslangicAy, int? bitisAy)
        {
            try
            {
                // ✅ Güncellenmiş parametreler
                var (stream, errorMessage) = await _auditReportService.GenerateExcelReport(cariGrupId, cariIds, il, ihaleNo, donem, baslangicAy, bitisAy);

                if (stream == null)
                {
                    TempData["Error"] = errorMessage;
                    return RedirectToAction("ExportToExcelView");
                }

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Fatura_Raporu.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Excel raporu oluşturulurken hata oluştu: {ex.Message}");
                TempData["Error"] = "Rapor oluşturulurken beklenmeyen bir hata oluştu.";
                return RedirectToAction("ExportToExcelView");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToPdf(int? cariGrupId, List<int>? cariIds, string? il, string? ihaleNo, int? donem, int? baslangicAy, int? bitisAy)
        {
            try
            {
                var (pdfStream, errorMessage) = await _auditReportService.GeneratePdfReport(cariGrupId, cariIds, il, ihaleNo, donem, baslangicAy, bitisAy);

                if (pdfStream == null)
                {
                    TempData["Error"] = errorMessage;
                    return RedirectToAction("ExportToExcelView");
                }

                return File(pdfStream, "application/pdf", "Fatura_Raporu.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError($"PDF raporu oluşturulurken hata oluştu: {ex.Message}");
                TempData["Error"] = "Rapor oluşturulurken beklenmeyen bir hata oluştu.";
                return RedirectToAction("ExportToExcelView");
            }
        }

    }
}
