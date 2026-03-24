using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
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

            // ── 2. Sayfa: Personel ──────────────────────────────────────
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
            string[] headers = { "Birim", "Görev/Unvan", "Sayı", "Ücret Tipi", "Aylık Brüt (₺)", "Süre (Gün)", "Toplam (₺)" };
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
                ws.Cell(row, 1).Value = p.Birim?.BirimKodu ?? "-";
                ws.Cell(row, 2).Value = p.GorevUnvan;
                ws.Cell(row, 3).Value = p.Sayi;
                ws.Cell(row, 4).Value = p.UcretTipi;
                ws.Cell(row, 5).Value = p.BirimFiyatAylik;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 6).Value = m.Ihale.IhaleSuresiGun;
                ws.Cell(row, 7).Value = p.ToplamTRY;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            // Toplam
            ws.Cell(row, 6).Value = "TOPLAM";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = m.PersonelToplam;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Style.Font.Bold = true;

            ws.Columns(1, 7).AdjustToContents();
        }

        private void EkGiderSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            string[] headers = { "Birim", "Gider Adı", "Kategori", "Miktar", "Birim Fiyat", "Para Birimi", "Kur", "Tutar (₺)" };
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
                ws.Cell(row, 1).Value = e.Birim?.BirimKodu ?? "-";
                ws.Cell(row, 2).Value = e.GiderAdi;
                ws.Cell(row, 3).Value = e.Kategori;
                ws.Cell(row, 4).Value = e.Miktar;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 5).Value = e.BirimFiyat;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 6).Value = e.ParaBirimi;
                ws.Cell(row, 7).Value = e.Kur;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 8).Value = e.TutarTRY;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            ws.Cell(row, 7).Value = "TOPLAM";
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 8).Value = m.EkGiderToplam;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Style.Font.Bold = true;

            ws.Columns(1, 8).AdjustToContents();
        }

        private void KalemSayfasiDoldur(IXLWorksheet ws, OzetViewModel m)
        {
            string[] headers = { "#", "Birim", "SUT Kodu", "Tetkik Adı", "Test Miktarı", "SUT Fiyat", "SUT Çarpan", "Toplam SUT", "Toplam SUT Fiyat" };
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
                ws.Cell(row, 2).Value = k.Birim?.BirimKodu ?? "-";
                ws.Cell(row, 3).Value = k.SutKodu;
                ws.Cell(row, 4).Value = k.TetkikAdi;
                ws.Cell(row, 5).Value = k.TestMiktari;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 6).Value = k.SutFiyat;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.0000";
                ws.Cell(row, 7).Value = k.SutCarpan;
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.000";
                ws.Cell(row, 8).Value = k.ToplamSUT;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 9).Value = k.ToplamSutFiyat;
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            ws.Cell(row, 8).Value = "TOPLAM";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = m.ToplamSutFiyat;
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Style.Font.Bold = true;

            ws.Columns(1, 9).AdjustToContents();
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

                    MRow("Personel Toplam", m.PersonelToplam);
                    MRow("Ek Gider Toplam", m.EkGiderToplam);
                    MRow("Ara Toplam", m.PersonelToplam + m.EkGiderToplam, true, "#f8f9fc");
                    MRow("Genel Gider", m.GenelGider);
                    MRow("Toplam SUT Fiyat (Çarpanlı)", m.ToplamSutFiyat);
                    MRow("GENEL TOPLAM", m.GenelToplam, true, "#eff6ff");

                    t.Cell().PaddingVertical(4).PaddingHorizontal(6).Text("Maliyet / SUT Puanı").SemiBold();
                    t.Cell().PaddingVertical(4).PaddingHorizontal(6).AlignRight().Text($"{m.ToplamMaliyetSutPuani:N4}").SemiBold();
                });

                // ── Personel ─────────────────────────────────────────
                if (m.Ihale.PersonelGiderler.Any())
                {
                    col.Item().PaddingTop(14).Text("Personel Giderleri").Bold().FontSize(10).FontColor("#3b82f6");
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(40); cd.RelativeColumn(3); cd.ConstantColumn(30);
                            cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(4)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(8);

                        Hdr("Birim"); Hdr("Görev/Unvan"); Hdr("Sayı"); Hdr("Ücret Tipi"); Hdr("Aylık Brüt"); Hdr("Toplam");

                        foreach (var p in m.Ihale.PersonelGiderler)
                        {
                            void Cell(string v) => t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                .PaddingVertical(3).PaddingHorizontal(4).Text(v).FontSize(8);
                            Cell(p.Birim?.BirimKodu ?? "-");
                            Cell(p.GorevUnvan);
                            Cell(p.Sayi.ToString());
                            Cell(p.UcretTipi);
                            Cell($"{p.BirimFiyatAylik:N2} ₺");
                            Cell($"{p.ToplamTRY:N2} ₺");
                        }

                        t.Cell().ColumnSpan(5).PaddingVertical(3).PaddingHorizontal(4)
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
                            cd.ConstantColumn(40); cd.RelativeColumn(3); cd.RelativeColumn(2);
                            cd.RelativeColumn(2); cd.ConstantColumn(35); cd.RelativeColumn(2);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(4)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(8);

                        Hdr("Birim"); Hdr("Gider Adı"); Hdr("Kategori"); Hdr("Birim Fiyat"); Hdr("PB"); Hdr("Tutar");

                        foreach (var e in m.Ihale.EkGiderler)
                        {
                            void Cell(string v) => t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                .PaddingVertical(3).PaddingHorizontal(4).Text(v).FontSize(8);
                            Cell(e.Birim?.BirimKodu ?? "-");
                            Cell(e.GiderAdi);
                            Cell(e.Kategori ?? "-");
                            Cell($"{e.BirimFiyat:N4}");
                            Cell(e.ParaBirimi);
                            Cell($"{e.TutarTRY:N2} ₺");
                        }

                        t.Cell().ColumnSpan(5).PaddingVertical(3).PaddingHorizontal(4)
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
                            cd.ConstantColumn(20); cd.ConstantColumn(40); cd.ConstantColumn(55);
                            cd.RelativeColumn(3); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2);
                        });

                        void Hdr(string v) => t.Cell().Background("#3b82f6").PaddingVertical(4).PaddingHorizontal(4)
                            .Text(v).FontColor("#ffffff").Bold().FontSize(7.5f);

                        Hdr("#"); Hdr("Birim"); Hdr("SUT Kodu"); Hdr("Tetkik Adı");
                        Hdr("Miktar"); Hdr("SUT Fiyat"); Hdr("Toplam SUT Fiyat");

                        foreach (var k in m.Ihale.IhaleKalemler.OrderBy(x => x.SiraNo))
                        {
                            void Cell(string v) => t.Cell().BorderBottom(1).BorderColor("#e3e8f0")
                                .PaddingVertical(3).PaddingHorizontal(4).Text(v).FontSize(7.5f);
                            Cell(k.SiraNo.ToString());
                            Cell(k.Birim?.BirimKodu ?? "-");
                            Cell(k.SutKodu ?? "-");
                            Cell(k.TetkikAdi);
                            Cell($"{k.TestMiktari:N0}");
                            Cell($"{k.SutFiyat:N4}");
                            Cell($"{k.ToplamSutFiyat:N2} ₺");
                        }

                        t.Cell().ColumnSpan(6).PaddingVertical(3).PaddingHorizontal(4)
                            .Text("TOPLAM").Bold().FontSize(8);
                        t.Cell().PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                            .Text($"{m.ToplamSutFiyat:N2} ₺").Bold().FontSize(8);
                    });
                }
            });
        }
    }
}
