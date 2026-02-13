using CRMOzem.Core.Enums;
using CRMOzem.Core.Interfaces.Inventory;
using CRMOzem.Core.Interfaces.Storage;
using CRMOzem.Core.Inventory;
using CRMOzem.DataAccess.Context; // senin DbContext namespace’in
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
namespace CRMOzem.Business.Services.Inventory;

public sealed class AssignmentPdfService : IAssignmentPdfService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IWebHostEnvironment _env;

    public AssignmentPdfService(ApplicationDbContext db, IFileStorage storage, IWebHostEnvironment env)
    {
        _db = db;
        _storage = storage;
        _env = env;
    }

    public async Task<(string FileKey, string FileName)> CreateAndStoreAsync(int assignmentId, CancellationToken ct)
    {
        var assignment = await _db.Assignments
            .AsNoTracking()
            .Include(x => x.Assignee).ThenInclude(s => s!.User)
            .Include(x => x.Items).ThenInclude(i => i.Asset)
            .FirstOrDefaultAsync(x => x.Id == assignmentId, ct);

        if (assignment is null)
            throw new InvalidOperationException("Zimmet bulunamadı.");

        var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "ozemZimmetFormu.pdf");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("PDF şablonu bulunamadı. Templates/ozemZimmetFormu.pdf bekleniyor.", templatePath);

        var outName = $"zimmet_{assignment.Id}.pdf";

        await using var ms = new MemoryStream();

        StampOnTemplate(assignment, templatePath, ms);

        ms.Position = 0;
        var fileKey = await _storage.SaveAsync(outName, "application/pdf", ms, ct);
        return (fileKey, outName);
    }

    private void StampOnTemplate(Assignment assignment, string templatePath, Stream output)
    {
        using var reader = new PdfReader(templatePath);
        using var stamper = new PdfStamper(reader, output);

        var cb = stamper.GetOverContent(1);
        var font = ResolveUnicodeFontOrFallback();
        const float size = 10f;

        var assignee = assignment.Assignee!;
        var fullName = !string.IsNullOrWhiteSpace(assignee.UserId)
            ? (assignee.User?.FullName ?? assignee.FullName)
            : assignee.FullName;

        var title = assignee.Title ?? "";
        var tcNo = assignee.TcNo ?? "";
        var date = assignment.AssignedAtUtc.ToLocalTime()
            .ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"));

        // Items -> Assets (TİPLİ)
        var assets = assignment.Items
            .Select(i => i.Asset)
            .Where(a => a != null)
            .Cast<Asset>()
            .ToList();

        var pc = assets.FirstOrDefault(a =>
            Contains(a.Category, "bilgisayar") || Contains(a.Category, "laptop") || Contains(a.Category, "pc"));

        var phone = assets.FirstOrDefault(a =>
            Contains(a.Category, "telefon") || Contains(a.Category, "phone"));

        // === Bilgisayar bloğu === (başlangıç koordinatları)
        if (pc is not null)
        {
            Write(cb, font, size, pc.Brand, 120, 755);
            Write(cb, font, size, pc.Model, 340, 755);
            Write(cb, font, size, pc.SerialNumber ?? "", 120, 725);

            Write(cb, font, size, fullName, 360, 690);
            Write(cb, font, size, title, 360, 670);
            Write(cb, font, size, date, 360, 650);
        }

        // === Telefon bloğu ===
        if (phone is not null)
        {
            Write(cb, font, size, phone.Brand, 120, 600);
            Write(cb, font, size, phone.Model, 340, 600);
            Write(cb, font, size, phone.ImeiNo ?? "", 120, 570);
            Write(cb, font, size, (phone.BarcodeNo ?? phone.InventoryTag ?? ""), 340, 570);

            Write(cb, font, size, fullName, 360, 535);
            Write(cb, font, size, title, 360, 515);
            Write(cb, font, size, tcNo, 360, 495);
            Write(cb, font, size, date, 360, 475);
        }

        // === Hat bloğu === (şimdilik Notes key/value ile)
        var hatOperator = ExtractFromNotes(assignment.Notes, "Operator");
        var hatNo = ExtractFromNotes(assignment.Notes, "HatNo");
        var intl = ExtractFromNotes(assignment.Notes, "Intl"); // Acik/Kapali

        if (!string.IsNullOrWhiteSpace(hatOperator) || !string.IsNullOrWhiteSpace(hatNo))
        {
            Write(cb, font, size, hatOperator ?? "", 120, 410);
            Write(cb, font, size, hatNo ?? "", 340, 410);

            if (EqualsTr(intl, "acik") || EqualsTr(intl, "açık"))
                Write(cb, font, 12f, "X", 205, 388);
            else if (EqualsTr(intl, "kapali") || EqualsTr(intl, "kapalı"))
                Write(cb, font, 12f, "X", 255, 388);

            Write(cb, font, size, date, 360, 388);

            Write(cb, font, size, fullName, 360, 345);
            Write(cb, font, size, title, 360, 325);
            Write(cb, font, size, tcNo, 360, 305);
            Write(cb, font, size, date, 360, 285);
        }

        stamper.FormFlattening = false;
    }

    private BaseFont ResolveUnicodeFontOrFallback()
    {
        // Türkçe karakter için font embed önerilir
        var fontPath = Path.Combine(_env.ContentRootPath, "Fonts", "DejaVuSans.ttf");
        if (File.Exists(fontPath))
            return BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

        return BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
    }

    private static void Write(PdfContentByte cb, BaseFont font, float size, string text, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        cb.BeginText();
        cb.SetFontAndSize(font, size);
        cb.ShowTextAligned(Element.ALIGN_LEFT, text, x, y, 0);
        cb.EndText();
    }

    private static bool Contains(string? value, string needle)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsTr(string? a, string b)
        => !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

    // "Operator=Turkcell;HatNo=05xx;Intl=Acik"
    private static string? ExtractFromNotes(string? notes, string key)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var parts = notes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            var kv = p.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
                return kv[1];
        }
        return null;
    }
}