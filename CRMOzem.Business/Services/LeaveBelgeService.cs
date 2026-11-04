
using CRMOzem.Business.Interfaces;
using CRMOzem.DataAccess.Context;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class LeaveBelgeService : ILeaveBelgeService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

        private static readonly BaseColor Ink = new BaseColor(15, 23, 42);
        private static readonly BaseColor Muted = new BaseColor(100, 116, 139);
        private static readonly BaseColor Line = new BaseColor(203, 213, 225);
        private static readonly BaseColor SoftBg = new BaseColor(248, 250, 252);

        public LeaveBelgeService(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<(byte[] Icerik, string DosyaAdi)> OlusturAsync(
            int leaveRequestId, CancellationToken ct = default)
        {
            var talep = await _db.LeaveRequests
                .AsNoTracking()
                .Include(l => l.User)
                .Include(l => l.LeaveType)
                .Include(l => l.Status)
                .FirstOrDefaultAsync(l => l.Id == leaveRequestId, ct);

            if (talep == null)
                throw new InvalidOperationException("İzin talebi bulunamadı.");

            if (talep.Status?.Code != "Approved")
                throw new InvalidOperationException("Yalnızca onaylanmış izinler için belge alınabilir.");

            var bf = ResolveFont();
            var fBaslik = new Font(bf, 15f, Font.BOLD, Ink);
            var fAltBaslik = new Font(bf, 9f, Font.NORMAL, Muted);
            var fEtiket = new Font(bf, 9f, Font.NORMAL, Muted);
            var fDeger = new Font(bf, 11f, Font.BOLD, Ink);
            var fMetin = new Font(bf, 10f, Font.NORMAL, Ink);
            var fKucuk = new Font(bf, 8f, Font.NORMAL, Muted);
            var fImza = new Font(bf, 9f, Font.NORMAL, Ink);

            using var ms = new MemoryStream();
            var doc = new Document(PageSize.A4, 50f, 50f, 45f, 45f);
            var writer = PdfWriter.GetInstance(doc, ms);
            writer.CloseStream = false;

            doc.Open();

            // ═══ BAŞLIK ═══
            var baslikTablo = new PdfPTable(2) { WidthPercentage = 100 };
            baslikTablo.SetWidths(new float[] { 65f, 35f });

            var solHucre = new PdfPCell { Border = Rectangle.NO_BORDER, PaddingBottom = 4f };
            solHucre.AddElement(new Paragraph("YILLIK İZİN BELGESİ", fBaslik));
            solHucre.AddElement(new Paragraph("ÖZEM Tıbbi Cihazlar", fAltBaslik));
            baslikTablo.AddCell(solHucre);

            var sagHucre = new PdfPCell { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT };
            sagHucre.AddElement(new Paragraph($"Belge No: İZN-{talep.Id:D5}", fKucuk)
            { Alignment = Element.ALIGN_RIGHT });
            sagHucre.AddElement(new Paragraph($"Düzenleme: {DateTime.Now:dd.MM.yyyy}", fKucuk)
            { Alignment = Element.ALIGN_RIGHT });
            baslikTablo.AddCell(sagHucre);

            doc.Add(baslikTablo);
            doc.Add(CizgiEkle(writer, 10f));

            // ═══ ÇALIŞAN BİLGİLERİ ═══
            doc.Add(BolumBasligi("ÇALIŞAN BİLGİLERİ", bf));

            var u = talep.User;
            var calisanTablo = YeniBilgiTablosu();
            BilgiSatiri(calisanTablo, "Adı Soyadı", u?.FullName ?? "—", fEtiket, fDeger);
            BilgiSatiri(calisanTablo, "Unvanı", Bos(u?.Title), fEtiket, fDeger);
            BilgiSatiri(calisanTablo, "Departmanı", Bos(u?.Departman), fEtiket, fDeger);
            BilgiSatiri(calisanTablo, "TC Kimlik No", Bos(u?.TcKimlikNo), fEtiket, fDeger);
            BilgiSatiri(calisanTablo, "İşe Giriş Tarihi",
                u?.IseGirisTarihi?.ToString("dd.MM.yyyy", TR) ?? "—", fEtiket, fDeger);
            doc.Add(calisanTablo);

            doc.Add(new Paragraph(" ") { SpacingAfter = 6f });

            // ═══ İZİN BİLGİLERİ ═══
            doc.Add(BolumBasligi("İZİN BİLGİLERİ", bf));

            var izinTablo = YeniBilgiTablosu();
            BilgiSatiri(izinTablo, "İzin Türü", talep.LeaveType?.Name ?? "—", fEtiket, fDeger);
            BilgiSatiri(izinTablo, "Başlangıç Tarihi",
                $"{talep.StartDate:dd.MM.yyyy} {talep.StartDate.ToString("dddd", TR)}", fEtiket, fDeger);
            BilgiSatiri(izinTablo, "Bitiş Tarihi",
                $"{talep.EndDate:dd.MM.yyyy} {talep.EndDate.ToString("dddd", TR)}", fEtiket, fDeger);
            BilgiSatiri(izinTablo, "Toplam Süre", $"{talep.TotalDays} iş günü", fEtiket, fDeger);
            BilgiSatiri(izinTablo, "İşbaşı Tarihi",
                IsbasiTarihi(talep.EndDate).ToString("dd.MM.yyyy dddd", TR), fEtiket, fDeger);

            if (talep.SaturdayExcluded)
                BilgiSatiri(izinTablo, "Not", "Cumartesi günleri izin süresinden sayılmamıştır.", fEtiket, fMetin);

            doc.Add(izinTablo);

            // ═══ AÇIKLAMA ═══
            if (!string.IsNullOrWhiteSpace(talep.Reason))
            {
                doc.Add(new Paragraph(" ") { SpacingAfter = 6f });
                doc.Add(BolumBasligi("AÇIKLAMA", bf));
                doc.Add(new Paragraph(talep.Reason.Trim(), fMetin)
                { SpacingBefore = 4f, SpacingAfter = 6f });
            }

            // ═══ ONAY BİLGİSİ ═══
            doc.Add(new Paragraph(" ") { SpacingAfter = 6f });
            doc.Add(BolumBasligi("ONAY", bf));

            var onayTablo = YeniBilgiTablosu();
            BilgiSatiri(onayTablo, "Talep Tarihi", talep.CreatedAt.ToString("dd.MM.yyyy HH:mm", TR), fEtiket, fDeger);
            BilgiSatiri(onayTablo, "Onaylayan", Bos(talep.DecisionByName), fEtiket, fDeger);
            BilgiSatiri(onayTablo, "Onay Tarihi",
                talep.DecisionAt?.ToString("dd.MM.yyyy HH:mm", TR) ?? "—", fEtiket, fDeger);

            if (!string.IsNullOrWhiteSpace(talep.DecisionNote))
                BilgiSatiri(onayTablo, "Onay Notu", talep.DecisionNote.Trim(), fEtiket, fMetin);

            doc.Add(onayTablo);

            // ═══ BEYAN ═══
            doc.Add(new Paragraph(" ") { SpacingAfter = 14f });

            var beyan = new Paragraph(
                $"Yukarıda belirtilen tarihler arasında {talep.TotalDays} iş günü yıllık ücretli iznimi " +
                "kullanacağımı, izin bitiminde işbaşı yapacağımı beyan ederim.",
                fMetin)
            {
                SpacingAfter = 26f,
                Alignment = Element.ALIGN_JUSTIFIED,
                IndentationLeft = 2f,
                IndentationRight = 2f
            };
            doc.Add(beyan);

            // ═══ İMZA ALANLARI ═══
            var imzaTablo = new PdfPTable(3) { WidthPercentage = 100, SpacingBefore = 10f };
            imzaTablo.SetWidths(new float[] { 33f, 33f, 34f });

            imzaTablo.AddCell(ImzaHucresi("ÇALIŞAN", u?.FullName ?? "", fKucuk, fImza));
            imzaTablo.AddCell(ImzaHucresi("İNSAN KAYNAKLARI", talep.DecisionByName ?? "", fKucuk, fImza));
            imzaTablo.AddCell(ImzaHucresi("ONAYLAYAN AMİR", "", fKucuk, fImza));

            doc.Add(imzaTablo);

            // ═══ DİPNOT ═══
            doc.Add(new Paragraph(" ") { SpacingAfter = 10f });
            doc.Add(new Paragraph(
                "Bu belge CRMOzem izin yönetim sistemi tarafından otomatik olarak düzenlenmiştir. " +
                $"Belge no İZN-{talep.Id:D5} ile sistem üzerinden doğrulanabilir.",
                fKucuk)
            { Alignment = Element.ALIGN_CENTER });

            doc.Close();

            var adSoyad = Temizle(u?.FullName ?? "calisan");
            var dosyaAdi = $"izin_belgesi_{adSoyad}_{talep.StartDate:yyyyMMdd}.pdf";

            return (ms.ToArray(), dosyaAdi);
        }

        // ═══════════════════════════════════════════════════════
        //  YARDIMCI
        // ═══════════════════════════════════════════════════════

        private BaseFont ResolveFont()
        {
            // 1) Projedeki gömülü font — AssignmentPdfService ile aynı dosya
            var path = Path.Combine(_env.ContentRootPath, "Fonts", "DejaVuSans.ttf");
            if (File.Exists(path))
                return BaseFont.CreateFont(path, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

            // 2) Windows sistem fontu — Türkçe karakterler korunur
            var sistem = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            if (File.Exists(sistem))
                return BaseFont.CreateFont(sistem, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

            // 3) Son çare — ş, ğ, İ, ı gibi karakterler bozulabilir
            return BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        }

        private static Paragraph BolumBasligi(string metin, BaseFont bf)
        {
            var f = new Font(bf, 9f, Font.BOLD, Muted);
            return new Paragraph(metin, f) { SpacingBefore = 4f, SpacingAfter = 6f };
        }

        private static PdfPTable YeniBilgiTablosu()
        {
            var t = new PdfPTable(2) { WidthPercentage = 100 };
            t.SetWidths(new float[] { 30f, 70f });
            return t;
        }

        private static void BilgiSatiri(PdfPTable t, string etiket, string deger, Font fEtiket, Font fDeger)
        {
            var e = new PdfPCell(new Phrase(etiket, fEtiket))
            {
                Border = Rectangle.BOTTOM_BORDER,
                BorderColorBottom = Line,
                BorderWidthBottom = 0.5f,
                PaddingTop = 6f,
                PaddingBottom = 6f,
                BackgroundColor = SoftBg,
                PaddingLeft = 8f,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };

            var d = new PdfPCell(new Phrase(deger, fDeger))
            {
                Border = Rectangle.BOTTOM_BORDER,
                BorderColorBottom = Line,
                BorderWidthBottom = 0.5f,
                PaddingTop = 6f,
                PaddingBottom = 6f,
                PaddingLeft = 10f,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };

            t.AddCell(e);
            t.AddCell(d);
        }

        private static PdfPCell ImzaHucresi(string baslik, string ad, Font fKucuk, Font fAd)
        {
            var c = new PdfPCell { Border = Rectangle.NO_BORDER, PaddingLeft = 4f, PaddingRight = 4f };

            c.AddElement(new Paragraph(baslik, fKucuk) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 34f });

            var cizgi = new Paragraph(new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(
                0.6f, 85f, Line, Element.ALIGN_CENTER, 0f)));
            c.AddElement(cizgi);

            c.AddElement(new Paragraph(string.IsNullOrWhiteSpace(ad) ? " " : ad, fAd)
            { Alignment = Element.ALIGN_CENTER, SpacingBefore = 4f });

            c.AddElement(new Paragraph("Ad Soyad / İmza", fKucuk) { Alignment = Element.ALIGN_CENTER });

            return c;
        }

        private static IElement CizgiEkle(PdfWriter writer, float bosluk)
        {
            return new Paragraph(new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(
                1f, 100f, Line, Element.ALIGN_CENTER, 0f)))
            { SpacingBefore = bosluk, SpacingAfter = bosluk };
        }

        /// <summary>İzin bitiminden sonraki ilk iş günü (pazar atlanır).</summary>
        private static DateTime IsbasiTarihi(DateTime izinBitisi)
        {
            var d = izinBitisi.Date.AddDays(1);
            while (d.DayOfWeek == DayOfWeek.Sunday)
                d = d.AddDays(1);
            return d;
        }

        private static string Bos(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

        private static string Temizle(string s)
        {
            var gecersiz = Path.GetInvalidFileNameChars();
            var temiz = new string(s.Where(c => !gecersiz.Contains(c)).ToArray());
            return temiz.Replace(' ', '_');
        }
    }
}
