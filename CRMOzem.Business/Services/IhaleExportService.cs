using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class IhaleExportService : IIhaleExportService
    {
        // ══════════════════════════════════════════════════════════════════
        // EXCEL
        // ══════════════════════════════════════════════════════════════════
        public byte[] ExcelOlustur(OzetViewModel model)
        {
            using var wb = new XLWorkbook();

            // ── 1. Sayfa: Özet ──────────────────────────────────────────
            var wsOzet = wb.AddWorksheet("Maliyet Özeti");
            OzetSayfasiDoldur(wsOzet, model);

            // ── 2. Sayfa: Kısım Bazlı Özet ──────────────────────────────
            if (model.KisimKirilimiVar)
            {
                var wsKisim = wb.AddWorksheet("Kısım Bazlı Özet");
                KisimSayfasiDoldur(wsKisim, model);
            }

            // ── 3. Sayfa: Personel ──────────────────────────────────────
            var wsPersonel = wb.AddWorksheet("Personel Giderleri");
            PersonelSayfasiDoldur(wsPersonel, model);

            // ── 3. Sayfa: Ek Giderler ───────────────────────────────────
            var wsEkGider = wb.AddWorksheet("Ek Giderler");
            EkGiderSayfasiDoldur(wsEkGider, model);

            // ── 4. Sayfa: İhale Kalemleri ───────────────────────────────
            var wsKalem = wb.AddWorksheet("İhale Kalemleri");
            KalemSayfasiDoldur(wsKalem, model);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private void OzetSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            var ihale = m.Ihale;

            // Başlık
            ws.Cell("A1").Value = "İHALE MALİYET ÖZETİ";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Range("A1:D1").Merge();

            ws.Cell("A2").Value = ihale.IhaleAdi;
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Range("A2:D2").Merge();

            // Parametreler
            int row = 4;
            void Param(string label, string val)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 2).Value = val;
                row++;
            }

            Param("Kayıt No", ihale.IhaleKayitNo ?? "—");
            Param("İhale Tarihi", ihale.IhaleTarihi?.ToString("dd.MM.yyyy") ?? "—");
            Param("İhale Şekli", ihale.IhaleSekli ?? "—");
            Param("Süre (Gün)", ihale.IhaleSuresiGun.ToString());
            Param("Şartname Kısım Sayısı", ihale.KisimSayisi.ToString());
            Param("Genel Gider Oranı", $"%{ihale.GenelGiderOran * 100:0.##}");
            Param("Kar Oranı", $"%{ihale.KarOran * 100:0.##}");
            Param("KDV Oranı", $"%{ihale.KDVOran * 100:0.##}");
            Param("USD Kuru", $"{ihale.KurUSD:N4} ₺");
            Param("EUR Kuru", $"{ihale.KurEUR:N4} ₺");

            row++;

            // Maliyet tablosu
            ws.Cell(row, 1).Value = "Kalem";
            ws.Cell(row, 2).Value = "Tutar (₺)";
            ws.Row(row).Style.Font.Bold = true;
            ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#3b82f6");
            ws.Row(row).Style.Font.FontColor = XLColor.White;
            row++;

            void MaliyetSatir(string label, decimal val, bool bold = false)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 2).Value = val;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                if (bold) { ws.Cell(row, 1).Style.Font.Bold = true; ws.Cell(row, 2).Style.Font.Bold = true; }
                row++;
            }

            MaliyetSatir("Personel Toplam", m.PersonelToplam);
            MaliyetSatir("Ek Gider Toplam", m.EkGiderToplam);
            MaliyetSatir("Ara Toplam", m.PersonelToplam + m.EkGiderToplam, true);
            MaliyetSatir("Genel Gider", m.GenelGider);
            MaliyetSatir("Toplam SUT Fiyat (Çarpanlı)", m.ToplamSutFiyat);
            MaliyetSatir("GENEL TOPLAM", m.GenelToplam, true);

            // Vurgula
            ws.Cell(row - 1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#eff6ff");
            ws.Cell(row - 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#eff6ff");

            row++;
            ws.Cell(row, 1).Value = "Toplam SUT Puanı";
            ws.Cell(row, 2).Value = m.ToplamMaliyetSutPuani;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0000";
            row++;
            ws.Cell(row, 1).Value = "Maliyet / SUT Puanı";
            ws.Cell(row, 2).Value = m.ToplamMaliyetSutPuani;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0000";

            ws.Columns(1, 4).AdjustToContents();
        }

        private void PersonelSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            string[] headers = { "Kısım", "Birim", "Görev/Unvan", "Sayı", "Ücret Tipi", "Aylık Brüt (₺)", "Süre (Gün)", "Toplam (₺)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#3b82f6");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var p in m.Ihale.PersonelGiderler)
            {
                ws.Cell(row, 1).Value = p.KisimEtiket;
                ws.Cell(row, 2).Value = p.Birim?.BirimKodu ?? "-";
                ws.Cell(row, 3).Value = p.GorevUnvan;
                ws.Cell(row, 4).Value = p.Sayi;
                ws.Cell(row, 5).Value = p.UcretTipi;
                ws.Cell(row, 6).Value = p.BirimFiyatAylik;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 7).Value = m.Ihale.IhaleSuresiGun;
                ws.Cell(row, 8).Value = p.ToplamTRY;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            // Kısım ara toplamları
            row = KisimAraToplam(ws, row, 8,
                m.Ihale.PersonelGiderler.Select(x => (x.KisimNo, x.ToplamTRY)));

            // Toplam
            ws.Cell(row, 7).Value = "TOPLAM";
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 8).Value = m.PersonelToplam;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Style.Font.Bold = true;

            ws.Columns(1, 8).AdjustToContents();
        }

        private void EkGiderSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            string[] headers = { "Kısım", "Birim", "Gider Adı", "Kategori", "Miktar", "Birim Fiyat", "Para Birimi", "Kur", "Tutar (₺)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#3b82f6");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var e in m.Ihale.EkGiderler)
            {
                ws.Cell(row, 1).Value = e.KisimEtiket;
                ws.Cell(row, 2).Value = e.Birim?.BirimKodu ?? "-";
                ws.Cell(row, 3).Value = e.GiderAdi;
                ws.Cell(row, 4).Value = e.Kategori;
                ws.Cell(row, 5).Value = e.Miktar;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 6).Value = e.BirimFiyat;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 7).Value = e.ParaBirimi;
                ws.Cell(row, 8).Value = e.Kur;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 9).Value = e.TutarTRY;
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            // Kısım ara toplamları
            row = KisimAraToplam(ws, row, 9,
                m.Ihale.EkGiderler.Select(x => (x.KisimNo, x.TutarTRY)));

            ws.Cell(row, 8).Value = "TOPLAM";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = m.EkGiderToplam;
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Style.Font.Bold = true;

            ws.Columns(1, 9).AdjustToContents();
        }

        private void KalemSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            string[] headers = { "#", "Kısım", "Birim", "SUT Kodu", "Tetkik Adı", "Test Miktarı", "SUT Fiyat", "SUT Çarpan", "Toplam SUT", "Toplam SUT Fiyat" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#3b82f6");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var k in m.Ihale.IhaleKalemler.OrderBy(x => x.SiraNo))
            {
                ws.Cell(row, 1).Value = k.SiraNo;
                ws.Cell(row, 2).Value = k.KisimEtiket;
                ws.Cell(row, 3).Value = k.Birim?.BirimKodu ?? "-";
                ws.Cell(row, 4).Value = k.SutKodu;
                ws.Cell(row, 5).Value = k.TetkikAdi;
                ws.Cell(row, 6).Value = k.TestMiktari;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Value = k.SutFiyat;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 8).Value = k.SutCarpan;
                ws.Cell(row, 8).Style.NumberFormat.Format = "0.000";
                ws.Cell(row, 9).Value = k.ToplamSUT;
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 10).Value = k.ToplamSutFiyat;
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            // Kısım ara toplamları
            row = KisimAraToplam(ws, row, 10,
                m.Ihale.IhaleKalemler.Select(x => (x.KisimNo, x.ToplamSutFiyat)));

            ws.Cell(row, 9).Value = "TOPLAM";
            ws.Cell(row, 9).Style.Font.Bold = true;
            ws.Cell(row, 10).Value = m.ToplamSutFiyat;
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Style.Font.Bold = true;

            ws.Columns(1, 10).AdjustToContents();
        }

        /// <summary>
        /// Detay sayfalarının altına kısım bazlı ara toplam satırları yazar.
        /// Kısma atanmamış satırlar "Ortak" başlığıyla ayrı gösterilir.
        /// </summary>
        private int KisimAraToplam(IXLWorksheet ws, int row, int tutarKolonu,
                                   IEnumerable<(int? KisimNo, decimal Tutar)> satirlar)
        {
            var liste = satirlar.ToList();
            if (liste.Count == 0) return row;

            var gruplar = liste
                .GroupBy(x => x.KisimNo)
                .OrderBy(g => g.Key ?? int.MaxValue)
                .ToList();

            // Tek grup varsa ara toplam anlamsız
            if (gruplar.Count < 2) return row;

            row++;
            foreach (var g in gruplar)
            {
                var etiket = g.Key.HasValue ? $"{g.Key}. Kısım ara toplamı" : "Ortak ara toplamı";
                ws.Cell(row, 1).Value = etiket;
                ws.Cell(row, 1).Style.Font.Italic = true;
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#4a5568");
                ws.Cell(row, tutarKolonu).Value = g.Sum(x => x.Tutar);
                ws.Cell(row, tutarKolonu).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, tutarKolonu).Style.Font.Italic = true;
                ws.Range(row, 1, row, tutarKolonu).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fc");
                row++;
            }

            return row;
        }

        /// <summary>Kısım bazlı özet sayfası.</summary>
        private void KisimSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            ws.Cell("A1").Value = "KISIM BAZLI MALİYET ÖZETİ";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 13;

            ws.Cell("A2").Value = m.Ihale.IhaleAdi;
            ws.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#4a5568");

            ws.Cell("A3").Value = $"Şartname kısım sayısı: {m.Ihale.KisimSayisi}";
            ws.Cell("A3").Style.Font.FontColor = XLColor.FromHtml("#4a5568");

            if (m.OrtakGiderVar)
            {
                ws.Cell("A4").Value = "Not: Kısma atanmamış satırlar kısımlara dağıtılmaz, \"Ortak Giderler\" satırında toplanır.";
                ws.Cell("A4").Style.Font.Italic = true;
                ws.Cell("A4").Style.Font.FontColor = XLColor.FromHtml("#92400e");
            }

            string[] headers =
            {
                "Kısım", "Kalem", "Test Miktarı", "SUT Puanı", "Personel (₺)",
                "Ek Gider (₺)", "Genel Gider (₺)", "SUT Fiyat (₺)", "Toplam Maliyet (₺)",
                "Pay (%)", "Maliyet / SUT"
            };

            int baslikSatiri = 6;
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(baslikSatiri, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#3b82f6");
                c.Style.Font.FontColor = XLColor.White;
            }

            int row = baslikSatiri + 1;

            void SatirYaz(KisimOzetSatir ks, bool ortak)
            {
                ws.Cell(row, 1).Value = ortak ? "Ortak Giderler" : ks.Ad + (ks.TanimDisi ? " (tanım dışı)" : "");
                ws.Cell(row, 2).Value = ks.KalemAdedi;
                ws.Cell(row, 3).Value = ks.TestMiktari;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 4).Value = ks.ToplamSutPuani;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = ks.PersonelTRY;
                ws.Cell(row, 6).Value = ks.EkGiderTRY;
                ws.Cell(row, 7).Value = ks.GenelGider;
                ws.Cell(row, 8).Value = ks.ToplamSutFiyat;
                ws.Cell(row, 9).Value = ks.ToplamMaliyet;
                ws.Range(row, 5, row, 9).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 9).Style.Font.Bold = true;
                ws.Cell(row, 10).Value = ks.PayYuzde;
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.0";
                ws.Cell(row, 11).Value = ks.MaliyetSutPuani;
                ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.0000";

                if (ortak)
                    ws.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f6");
                else if (ks.TanimDisi)
                    ws.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#fff5f5");
                else if (!ks.KayitVar)
                    ws.Range(row, 1, row, 11).Style.Font.FontColor = XLColor.FromHtml("#a0aec0");

                row++;
            }

            foreach (var ks in m.KisimOzetler) SatirYaz(ks, false);
            if (m.OrtakGiderVar) SatirYaz(m.OrtakOzet, true);

            // Dip toplam
            ws.Cell(row, 1).Value = "GENEL TOPLAM";
            ws.Cell(row, 2).Value = m.Ihale.IhaleKalemler.Count;
            ws.Cell(row, 3).Value = m.Ihale.IhaleKalemler.Sum(x => x.TestMiktari);
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 4).Value = m.ToplamSutPuani;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = m.PersonelToplam;
            ws.Cell(row, 6).Value = m.EkGiderToplam;
            ws.Cell(row, 7).Value = m.GenelGider;
            ws.Cell(row, 8).Value = m.ToplamSutFiyat;
            ws.Cell(row, 9).Value = m.GenelToplam;
            ws.Range(row, 5, row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Value = 100;
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 11).Value = m.ToplamMaliyetSutPuani;
            ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.0000";

            ws.Range(row, 1, row, 11).Style.Font.Bold = true;
            ws.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#eff6ff");

            ws.SheetView.FreezeRows(baslikSatiri);
            ws.Columns(1, 11).AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════════════
        // PDF (QuestPDF)
        // ══════════════════════════════════════════════════════════════════
        public byte[] PdfOlustur(OzetViewModel model)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                    page.Header().Element(c => PdfHeader(c, model));
                    page.Content().Element(c => PdfContent(c, model));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                        x.Span($"   —   {DateTime.Now:dd.MM.yyyy HH:mm}");
                    });
                });
            }).GeneratePdf();
        }

        private void PdfHeader(IContainer c, OzetViewModel m)
        {
            c.BorderBottom(1).BorderColor("#3b82f6").PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("İHALE MALİYET ÖZETİ")
                        .FontSize(14).Bold().FontColor("#1a202c");
                    col.Item().Text(m.Ihale.IhaleAdi)
                        .FontSize(10).FontColor("#4a5568");
                });
                row.ConstantItem(120).AlignRight().Column(col =>
                {
                    if (!string.IsNullOrEmpty(m.Ihale.IhaleKayitNo))
                        col.Item().Text(m.Ihale.IhaleKayitNo).FontSize(9).FontColor("#4a5568");
                    if (m.Ihale.IhaleTarihi.HasValue)
                        col.Item().Text(m.Ihale.IhaleTarihi.Value.ToString("dd.MM.yyyy")).FontSize(9).FontColor("#4a5568");
                });
            });
        }

        private void PdfContent(IContainer c, OzetViewModel m)
        {
            c.Column(col =>
            {
                // ── Parametreler ──────────────────────────────────────
                col.Item().PaddingTop(10).Text("Parametreler").Bold().FontSize(10).FontColor("#3b82f6");
                col.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                    void PRow(string l, string v)
                    {
                        t.Cell().PaddingVertical(3).Text(l).SemiBold().FontColor("#4a5568");
                        t.Cell().PaddingVertical(3).Text(v).FontColor("#1a202c");
                    }
                    PRow("Süre", $"{m.Ihale.IhaleSuresiGun} gün");
                    PRow("Kısım Sayısı", m.Ihale.KisimSayisi.ToString());
                    PRow("Şekil", m.Ihale.IhaleSekli ?? "—");
                    PRow("Genel Gider", $"%{m.Ihale.GenelGiderOran * 100:0.##}");
                    PRow("KDV", $"%{m.Ihale.KDVOran * 100:0.##}");
                    PRow("USD", $"{m.Ihale.KurUSD:N2} ₺");
                    PRow("EUR", $"{m.Ihale.KurEUR:N2} ₺");
                });

                // ── Maliyet Özeti ────────────────────────────────────
                col.Item().PaddingTop(14).Text("Maliyet Özeti").Bold().FontSize(10).FontColor("#3b82f6");
                col.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); });

                    void MRow(string l, decimal v, bool bold = false, string? bg = null)
                    {
                        var c1 = t.Cell().PaddingVertical(4).PaddingHorizontal(6);
                        var c2 = t.Cell().PaddingVertical(4).PaddingHorizontal(6).AlignRight();
                        if (bg != null) { c1 = c1.Background(bg); c2 = c2.Background(bg); }
                        var t1 = c1.Text(l); var t2 = c2.Text($"{v:N2} ₺");
                        if (bold) { t1.Bold(); t2.Bold(); }
                    }

                    // Tutar satırı (₺)
                    MRow("Personel Toplam", m.PersonelToplam);
                    MRow("Ek Gider Toplam", m.EkGiderToplam);
                    MRow("Ara Toplam", m.PersonelToplam + m.EkGiderToplam, true, "#f8f9fc");
                    MRow("Genel Gider", m.GenelGider);
                    MRow("Toplam SUT Fiyat (Çarpanlı)", m.ToplamSutFiyat);
                    MRow("GENEL TOPLAM", m.GenelToplam, true, "#eff6ff");

                    // Miktar satırı (birimsiz) — para birimi eklenmez
                    void QRow(string l, string v, bool bold = false, string? bg = null)
                    {
                        var c1 = t.Cell().PaddingVertical(4).PaddingHorizontal(6);
                        var c2 = t.Cell().PaddingVertical(4).PaddingHorizontal(6).AlignRight();
                        if (bg != null) { c1 = c1.Background(bg); c2 = c2.Background(bg); }
                        var t1 = c1.Text(l); var t2 = c2.Text(v);
                        if (bold) { t1.Bold(); t2.Bold(); }
                    }

                    QRow("Toplam Kalem Sayısı", $"{m.Ihale.IhaleKalemler.Count:N0}");
                    QRow("Toplam Test Miktarı", $"{m.Ihale.IhaleKalemler.Sum(x => x.TestMiktari):N0}", true, "#f8f9fc");
                    QRow("Toplam SUT Puanı", $"{m.ToplamSutPuani:N2}", true, "#f8f9fc");

                    t.Cell().PaddingVertical(4).PaddingHorizontal(6).Text("Maliyet / SUT Puanı").SemiBold();
                    t.Cell().PaddingVertical(4).PaddingHorizontal(6).AlignRight().Text($"{m.ToplamMaliyetSutPuani:N4}").SemiBold();
                });

                // ── Kısım Bazlı Özet ─────────────────────────────────
                if (m.KisimKirilimiVar)
                {
                    col.Item().PaddingTop(14).Text("Kısım Bazlı Özet").Bold().FontSize(10).FontColor("#3b82f6");

                    if (m.OrtakGiderVar)
                    {
                        col.Item().PaddingTop(2).Text("Kısma atanmamış giderler kısımlara dağıtılmadı; \"Ortak Giderler\" satırında toplandı.")
                            .FontSize(7.5f).Italic().FontColor("#92400e");
                    }

                    col.Item().PaddingTop(4).Table(t =>
                    {
                        // Genel gider kolonu bilerek yok: (personel + ek gider) × oran olarak
                        // türetilebiliyor ve oran parametrelerde yazılı. Yerine test miktarı
                        // ve SUT puanı gösteriliyor. Tutarlar tam lira (kuruşsuz).
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2.2f); cd.ConstantColumn(28); cd.ConstantColumn(44);
                            cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2);
                            cd.RelativeColumn(2); cd.RelativeColumn(2.2f); cd.ConstantColumn(32);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(3)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(7f);

                        Hdr("Kısım"); Hdr("Kalem"); Hdr("Test Miktarı"); Hdr("SUT Puanı");
                        Hdr("Personel ₺"); Hdr("Ek Gider ₺"); Hdr("SUT Fiyat ₺");
                        Hdr("Toplam Maliyet ₺"); Hdr("Pay");

                        void KisimSatir(KisimOzetSatir ks, string? bg)
                        {
                            void Cell(string v, bool bold = false, bool saga = true)
                            {
                                var cell = t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                    .PaddingVertical(3).PaddingHorizontal(3);
                                if (bg != null) cell = cell.Background(bg);
                                if (saga) cell = cell.AlignRight();
                                var txt = cell.Text(v).FontSize(7f);
                                if (bold) txt.Bold();
                            }

                            Cell(ks.Ad + (ks.TanimDisi ? " (tanım dışı)" : ""), false, false);
                            Cell(ks.KalemAdedi > 0 ? ks.KalemAdedi.ToString("N0") : "—");
                            Cell(ks.TestMiktari > 0 ? $"{ks.TestMiktari:N0}" : "—");
                            Cell(ks.ToplamSutPuani > 0 ? $"{ks.ToplamSutPuani:N0}" : "—");
                            Cell(ks.PersonelTRY > 0 ? $"{ks.PersonelTRY:N0}" : "—");
                            Cell(ks.EkGiderTRY > 0 ? $"{ks.EkGiderTRY:N0}" : "—");
                            Cell(ks.ToplamSutFiyat > 0 ? $"{ks.ToplamSutFiyat:N0}" : "—");
                            Cell($"{ks.ToplamMaliyet:N0}", true);
                            Cell(ks.KayitVar ? $"%{ks.PayYuzde:N1}" : "—");
                        }

                        foreach (var ks in m.KisimOzetler)
                            KisimSatir(ks, ks.TanimDisi ? "#fff5f5" : (ks.KayitVar ? null : "#fcfcfd"));

                        if (m.OrtakGiderVar)
                            KisimSatir(m.OrtakOzet, "#f3f4f6");

                        // Dip toplam
                        void TCell(string v, bool saga = true)
                        {
                            var cell = t.Cell().Background("#eff6ff").PaddingVertical(3).PaddingHorizontal(3);
                            if (saga) cell = cell.AlignRight();
                            cell.Text(v).Bold().FontSize(7f);
                        }

                        TCell("GENEL TOPLAM", false);
                        TCell(m.Ihale.IhaleKalemler.Count.ToString("N0"));
                        TCell($"{m.Ihale.IhaleKalemler.Sum(x => x.TestMiktari):N0}");
                        TCell($"{m.ToplamSutPuani:N0}");
                        TCell($"{m.PersonelToplam:N0}");
                        TCell($"{m.EkGiderToplam:N0}");
                        TCell($"{m.ToplamSutFiyat:N0}");
                        TCell($"{m.GenelToplam:N0}");
                        TCell("%100,0");
                    });

                    col.Item().PaddingTop(3).Text(
                        $"Genel gider kısım içinde (personel + ek gider) × %{m.Ihale.GenelGiderOran * 100:0.##} olarak hesaplandı ve toplam maliyete dahildir.")
                        .FontSize(6.5f).Italic().FontColor("#718096");
                }

                // ── Personel ─────────────────────────────────────────
                if (m.Ihale.PersonelGiderler.Any())
                {
                    col.Item().PaddingTop(14).Text("Personel Giderleri").Bold().FontSize(10).FontColor("#3b82f6");
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(52); cd.ConstantColumn(40); cd.RelativeColumn(3); cd.ConstantColumn(30);
                            cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(4)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(8);

                        Hdr("Kısım"); Hdr("Birim"); Hdr("Görev/Unvan"); Hdr("Sayı"); Hdr("Ücret Tipi"); Hdr("Aylık Brüt"); Hdr("Toplam");

                        foreach (var p in m.Ihale.PersonelGiderler)
                        {
                            void Cell(string v) => t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                .PaddingVertical(3).PaddingHorizontal(4).Text(v).FontSize(8);
                            Cell(p.KisimEtiket);
                            Cell(p.Birim?.BirimKodu ?? "-");
                            Cell(p.GorevUnvan);
                            Cell(p.Sayi.ToString());
                            Cell(p.UcretTipi);
                            Cell($"{p.BirimFiyatAylik:N2} ₺");
                            Cell($"{p.ToplamTRY:N2} ₺");
                        }

                        t.Cell().ColumnSpan(6).PaddingVertical(3).PaddingHorizontal(4)
                            .Text("TOPLAM").Bold().FontSize(8);
                        t.Cell().PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                            .Text($"{m.PersonelToplam:N2} ₺").Bold().FontSize(8);
                    });
                }

                // ── Ek Giderler ──────────────────────────────────────
                if (m.Ihale.EkGiderler.Any())
                {
                    col.Item().PaddingTop(14).Text("Ek Giderler").Bold().FontSize(10).FontColor("#3b82f6");
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(52); cd.ConstantColumn(40); cd.RelativeColumn(3); cd.RelativeColumn(2);
                            cd.RelativeColumn(2); cd.ConstantColumn(35); cd.RelativeColumn(2);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(4)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(8);

                        Hdr("Kısım"); Hdr("Birim"); Hdr("Gider Adı"); Hdr("Kategori"); Hdr("Birim Fiyat"); Hdr("PB"); Hdr("Tutar");

                        foreach (var e in m.Ihale.EkGiderler)
                        {
                            void Cell(string v) => t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                .PaddingVertical(3).PaddingHorizontal(4).Text(v).FontSize(8);
                            Cell(e.KisimEtiket);
                            Cell(e.Birim?.BirimKodu ?? "-");
                            Cell(e.GiderAdi);
                            Cell(e.Kategori ?? "-");
                            Cell($"{e.BirimFiyat:N4}");
                            Cell(e.ParaBirimi);
                            Cell($"{e.TutarTRY:N2} ₺");
                        }

                        t.Cell().ColumnSpan(6).PaddingVertical(3).PaddingHorizontal(4)
                            .Text("TOPLAM").Bold().FontSize(8);
                        t.Cell().PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                            .Text($"{m.EkGiderToplam:N2} ₺").Bold().FontSize(8);
                    });
                }

                // ── İhale Kalemleri ──────────────────────────────────
                if (m.Ihale.IhaleKalemler.Any())
                {
                    col.Item().PaddingTop(14).Text("İhale Kalemleri").Bold().FontSize(10).FontColor("#3b82f6");
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(20); cd.ConstantColumn(48); cd.ConstantColumn(40); cd.ConstantColumn(55);
                            cd.RelativeColumn(3); cd.RelativeColumn(1.9f); cd.RelativeColumn(1.9f);
                            cd.RelativeColumn(1.9f); cd.RelativeColumn(1.9f);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(4)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(7.5f);

                        Hdr("#"); Hdr("Kısım"); Hdr("Birim"); Hdr("SUT Kodu"); Hdr("Tetkik Adı");
                        Hdr("Test Miktarı"); Hdr("SUT Fiyat"); Hdr("Toplam SUT Puanı"); Hdr("Toplam SUT Fiyat");

                        foreach (var k in m.Ihale.IhaleKalemler.OrderBy(x => x.SiraNo))
                        {
                            void Cell(string v, bool saga = false)
                            {
                                var cell = t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                    .PaddingVertical(3).PaddingHorizontal(4);
                                if (saga) cell = cell.AlignRight();
                                cell.Text(v).FontSize(7.5f);
                            }
                            Cell(k.SiraNo.ToString());
                            Cell(k.KisimEtiket);
                            Cell(k.Birim?.BirimKodu ?? "-");
                            Cell(k.SutKodu ?? "-");
                            Cell(k.TetkikAdi);
                            Cell($"{k.TestMiktari:N0}", true);
                            Cell($"{k.SutFiyat:N4}", true);
                            Cell($"{k.ToplamSUT:N2}", true);
                            Cell($"{k.ToplamSutFiyat:N2} ₺", true);
                        }

                        // Dip toplam: miktar ve SUT puanı kendi kolonlarının altında
                        void KTot(string v, bool saga = true)
                        {
                            var cell = t.Cell().Background("#eff6ff").PaddingVertical(3).PaddingHorizontal(4);
                            if (saga) cell = cell.AlignRight();
                            cell.Text(v).Bold().FontSize(8);
                        }

                        t.Cell().ColumnSpan(5).Background("#eff6ff").PaddingVertical(3).PaddingHorizontal(4)
                            .Text("TOPLAM").Bold().FontSize(8);
                        KTot($"{m.Ihale.IhaleKalemler.Sum(x => x.TestMiktari):N0}");
                        KTot("");
                        KTot($"{m.ToplamSutPuani:N2}");
                        KTot($"{m.ToplamSutFiyat:N2} ₺");
                    });
                }
            });
        }
    }
}
