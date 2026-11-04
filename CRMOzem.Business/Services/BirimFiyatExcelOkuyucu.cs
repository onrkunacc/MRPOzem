using ClosedXML.Excel;
using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs;
using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class BirimFiyatExcelOkuyucu : IBirimFiyatExcelOkuyucu
    {
        private readonly ILogger<BirimFiyatExcelOkuyucu> _log;

        private const int OrnekSatirSayisi = 8;
        private const int MaksUyari = 50;

        public BirimFiyatExcelOkuyucu(ILogger<BirimFiyatExcelOkuyucu> log)
        {
            _log = log;
        }

       
        public ExcelOnizlemeDto Onizle(Stream dosya, string dosyaAdi, string? sayfaAdi = null, int baslikSatiri = 1)
        {
            var dto = new ExcelOnizlemeDto { DosyaAdi = dosyaAdi };

            try
            {
                if (dosya.CanSeek) dosya.Seek(0, SeekOrigin.Begin);

                using var wb = new XLWorkbook(dosya);

                dto.SayfaAdlari = wb.Worksheets.Select(w => w.Name).ToList();
                if (dto.SayfaAdlari.Count == 0)
                {
                    dto.Hata = "Dosyada okunabilir sayfa bulunamadı.";
                    return dto;
                }

                var ws = !string.IsNullOrWhiteSpace(sayfaAdi)
                    ? wb.Worksheets.FirstOrDefault(w => w.Name == sayfaAdi) ?? wb.Worksheet(1)
                    : wb.Worksheet(1);

                dto.SecilenSayfa = ws.Name;

                if (baslikSatiri < 1) baslikSatiri = 1;

                var sonSatir = ws.LastRowUsed()?.RowNumber() ?? 0;
                var sonKolon = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

                if (sonSatir == 0 || sonKolon == 0)
                {
                    dto.Hata = $"'{ws.Name}' sayfası boş görünüyor.";
                    return dto;
                }

                if (baslikSatiri > sonSatir)
                {
                    dto.Hata = $"Başlık satırı {baslikSatiri} olarak verildi ama sayfada {sonSatir} satır var.";
                    return dto;
                }

               
                for (var k = 1; k <= sonKolon; k++)
                {
                    var metin = ws.Cell(baslikSatiri, k).GetString()?.Trim();
                    dto.Basliklar.Add(string.IsNullOrWhiteSpace(metin) ? $"Kolon {k}" : metin);
                }

               
                var ilkVeri = baslikSatiri + 1;
                var sonOrnek = Math.Min(sonSatir, ilkVeri + OrnekSatirSayisi - 1);

                for (var s = ilkVeri; s <= sonOrnek; s++)
                {
                    var satir = new List<string>();
                    for (var k = 1; k <= sonKolon; k++)
                        satir.Add(HucreMetni(ws.Cell(s, k)));

                    if (satir.Any(h => !string.IsNullOrWhiteSpace(h)))
                        dto.OrnekSatirlar.Add(satir);
                }

                dto.ToplamVeriSatiri = Math.Max(0, sonSatir - baslikSatiri);

                
                dto.Tahmin = KolonlariTahminEt(dto.Basliklar);
                dto.Tahmin.SayfaAdi = ws.Name;
                dto.Tahmin.BaslikSatiri = baslikSatiri;

                dto.Basarili = true;
                return dto;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Excel ön izleme hatası. Dosya: {Dosya}", dosyaAdi);
                dto.Hata = "Dosya okunamadı: " + EnIcMesaj(ex) +
                           "  (Yalnızca .xlsx desteklenir — eski .xls dosyalarını Excel'de 'Farklı Kaydet → .xlsx' ile çevirin.)";
                return dto;
            }
        }

       

        public ExcelOkumaSonucu Oku(
            Stream dosya,
            KolonEslesmeDto eslesme,
            KarsilastirmaTarafi taraf,
            string paraBirimi,
            out List<BirimFiyatSatir> satirlar)
        {
            satirlar = new List<BirimFiyatSatir>();
            var sonuc = new ExcelOkumaSonucu();

            if (eslesme == null || !eslesme.Gecerli)
            {
                sonuc.Hata = "Referans ve birim fiyat kolonları seçilmeden dosya okunamaz.";
                return sonuc;
            }

            try
            {
                if (dosya.CanSeek) dosya.Seek(0, SeekOrigin.Begin);

                using var wb = new XLWorkbook(dosya);

                var ws = !string.IsNullOrWhiteSpace(eslesme.SayfaAdi)
                    ? wb.Worksheets.FirstOrDefault(w => w.Name == eslesme.SayfaAdi) ?? wb.Worksheet(1)
                    : wb.Worksheet(1);

                var sonSatir = ws.LastRowUsed()?.RowNumber() ?? 0;
                var baslik = Math.Max(1, eslesme.BaslikSatiri);

                for (var s = baslik + 1; s <= sonSatir; s++)
                {
                    var referansHam = Metin(ws, s, eslesme.ReferansKolon);
                    var fiyatHucresi = Hucre(ws, s, eslesme.BirimFiyatKolon);

                    
                    var urunHam = Metin(ws, s, eslesme.UrunAdiKolon);
                    if (string.IsNullOrWhiteSpace(referansHam) &&
                        string.IsNullOrWhiteSpace(urunHam) &&
                        (fiyatHucresi == null || fiyatHucresi.IsEmpty()))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(referansHam))
                    {
                        sonuc.AtlananSatir++;
                        UyariEkle(sonuc, $"Satır {s}: referans numarası boş, atlandı.");
                        continue;
                    }

                    var fiyat = SayiCevir(fiyatHucresi);
                    if (fiyat == null)
                    {
                        sonuc.AtlananSatir++;
                        UyariEkle(sonuc, $"Satır {s}: birim fiyat okunamadı ('{HucreMetni(fiyatHucresi)}'), atlandı.");
                        continue;
                    }

                    var tarih = eslesme.TarihKolon >= 0 ? TarihCevir(Hucre(ws, s, eslesme.TarihKolon)) : null;

                    var satir = new BirimFiyatSatir
                    {
                        Taraf = taraf,
                        ReferansNo = Kirp(referansHam, 100),
                        ReferansNormalize = ReferansNormalizeEt(referansHam),
                        UrunAdi = Kirp(urunHam, 500),
                        BirimFiyat = fiyat.Value,
                        ParaBirimi = paraBirimi,
                        FaturaTarihi = tarih,
                        Donem = tarih.HasValue ? new DateTime(tarih.Value.Year, tarih.Value.Month, 1) : null,
                        KaynakSatirNo = s
                    };

                    if (eslesme.MiktarKolon >= 0)
                        satir.Miktar = SayiCevir(Hucre(ws, s, eslesme.MiktarKolon));

                    if (eslesme.BirimKolon >= 0)
                        satir.Birim = Kirp(Metin(ws, s, eslesme.BirimKolon), 50);

                    if (eslesme.FaturaNoKolon >= 0)
                        satir.FaturaNo = Kirp(Metin(ws, s, eslesme.FaturaNoKolon), 300);

                    if (eslesme.KurKolon >= 0)
                    {
                        var kur = SayiCevir(Hucre(ws, s, eslesme.KurKolon));
                        if (kur.HasValue && kur.Value > 0)
                        {
                            satir.Kur = kur;
                            satir.KurTarihi = tarih;
                        }
                    }

                    if (!tarih.HasValue && eslesme.TarihKolon >= 0)
                    {
                        satir.Uyari = "Fatura tarihi okunamadı.";
                        UyariEkle(sonuc, $"Satır {s}: fatura tarihi okunamadı, satır tarihsiz alındı.");
                    }

                    if (fiyat.Value <= 0)
                    {
                        satir.Uyari = string.IsNullOrEmpty(satir.Uyari)
                            ? "Birim fiyat sıfır veya negatif."
                            : satir.Uyari + " Birim fiyat sıfır veya negatif.";
                    }

                    satirlar.Add(satir);
                    sonuc.OkunanSatir++;
                }

                if (sonuc.OkunanSatir == 0)
                {
                    sonuc.Hata = "Dosyadan hiç geçerli satır okunamadı. Başlık satırı ve kolon eşleşmesini kontrol edin.";
                    return sonuc;
                }

                sonuc.Basarili = true;
                _log.LogInformation("Excel okundu: {Okunan} satır, {Atlanan} atlandı. Taraf={Taraf}",
                    sonuc.OkunanSatir, sonuc.AtlananSatir, taraf);

                return sonuc;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Excel okuma hatası.");
                sonuc.Hata = "Dosya okunamadı: " + EnIcMesaj(ex);
                return sonuc;
            }
        }

   

        private static readonly string[] ReferansAnahtarlari =
            { "referans", "ref no", "refno", "ref", "urun kodu", "ürün kodu", "stok kodu", "malzeme kodu",
              "katalog", "product code", "item code", "sku", "barkod", "sut kod", "kod" };

        private static readonly string[] UrunAnahtarlari =
            { "urun adi", "ürün adı", "urun", "ürün", "malzeme adi", "malzeme adı", "aciklama", "açıklama",
              "product name", "description", "tanim", "tanım", "isim", "ad" };

        private static readonly string[] FiyatAnahtarlari =
            { "birim fiyat", "b.fiyat", "bfiyat", "unit price", "birim fiyati", "birim fiyatı",
              "fiyat", "price", "tutar/adet", "birim tutar" };

        private static readonly string[] TarihAnahtarlari =
            { "fatura tarihi", "belge tarihi", "tarih", "invoice date", "date", "duzenleme tarihi", "düzenleme tarihi" };

        private static readonly string[] MiktarAnahtarlari =
            { "miktar", "adet", "qty", "quantity", "sayi", "sayı" };

        private static readonly string[] FaturaNoAnahtarlari =
            { "fatura no", "belge no", "invoice no", "invoice number", "fatura numarasi", "fatura numarası" };

        private static readonly string[] KurAnahtarlari =
            { "kur", "doviz kuru", "döviz kuru", "exchange rate", "rate", "usd/try", "usdtry" };

        private static readonly string[] BirimAnahtarlari =
            { "birim", "olcu birimi", "ölçü birimi", "unit", "uom" };

        private static KolonEslesmeDto KolonlariTahminEt(List<string> basliklar)
        {
            var dto = new KolonEslesmeDto();
            var normalize = basliklar.Select(NormalizeBaslik).ToList();

            dto.ReferansKolon = EnIyiEslesme(normalize, ReferansAnahtarlari);
            dto.UrunAdiKolon = EnIyiEslesme(normalize, UrunAnahtarlari, dislananlar: new[] { dto.ReferansKolon });
            dto.BirimFiyatKolon = EnIyiEslesme(normalize, FiyatAnahtarlari);
            dto.TarihKolon = EnIyiEslesme(normalize, TarihAnahtarlari);
            dto.MiktarKolon = EnIyiEslesme(normalize, MiktarAnahtarlari);
            dto.FaturaNoKolon = EnIyiEslesme(normalize, FaturaNoAnahtarlari, dislananlar: new[] { dto.ReferansKolon });
            dto.KurKolon = EnIyiEslesme(normalize, KurAnahtarlari);
            dto.BirimKolon = EnIyiEslesme(normalize, BirimAnahtarlari, dislananlar: new[] { dto.BirimFiyatKolon });

            return dto;
        }

      
        private static int EnIyiEslesme(List<string> basliklar, string[] anahtarlar, int[]? dislananlar = null)
        {
            var disla = dislananlar ?? Array.Empty<int>();

            
            for (var a = 0; a < anahtarlar.Length; a++)
                for (var i = 0; i < basliklar.Count; i++)
                    if (!disla.Contains(i) && basliklar[i] == anahtarlar[a])
                        return i;

            
            for (var a = 0; a < anahtarlar.Length; a++)
                for (var i = 0; i < basliklar.Count; i++)
                    if (!disla.Contains(i) && basliklar[i].StartsWith(anahtarlar[a], StringComparison.Ordinal))
                        return i;

           
            for (var a = 0; a < anahtarlar.Length; a++)
                for (var i = 0; i < basliklar.Count; i++)
                    if (!disla.Contains(i) && basliklar[i].Contains(anahtarlar[a], StringComparison.Ordinal))
                        return i;

            return -1;
        }

        private static string NormalizeBaslik(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            var t = s.Trim().ToLowerInvariant()
                .Replace("ı", "i").Replace("İ", "i").Replace("i̇", "i")
                .Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s")
                .Replace("ö", "o").Replace("ç", "c")
                .Replace("_", " ").Replace("-", " ").Replace(".", " ");

           
            var sb = new StringBuilder(t.Length);
            var oncekiBosluk = false;
            foreach (var ch in t)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!oncekiBosluk) sb.Append(' ');
                    oncekiBosluk = true;
                }
                else
                {
                    sb.Append(ch);
                    oncekiBosluk = false;
                }
            }

            return sb.ToString().Trim();
        }

    

        private static IXLCell? Hucre(IXLWorksheet ws, int satir, int kolonIndex0)
            => kolonIndex0 < 0 ? null : ws.Cell(satir, kolonIndex0 + 1);

        private static string Metin(IXLWorksheet ws, int satir, int kolonIndex0)
            => HucreMetni(Hucre(ws, satir, kolonIndex0));

        private static string HucreMetni(IXLCell? cell)
        {
            if (cell == null || cell.IsEmpty()) return "";
            try { return cell.GetFormattedString()?.Trim() ?? ""; }
            catch { return cell.GetString()?.Trim() ?? ""; }
        }

      
        private static decimal? SayiCevir(IXLCell? cell)
        {
            if (cell == null || cell.IsEmpty()) return null;

            if (cell.TryGetValue<double>(out var d) && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                try { return Convert.ToDecimal(d); }
                catch { /* taşma — metin olarak dene */ }
            }

            var ham = cell.GetString();
            return MetniSayiyaCevir(ham);
        }

       
        public static decimal? MetniSayiyaCevir(string? ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return null;

           
            var temiz = new StringBuilder(ham.Length);
            var negatif = ham.Contains('-') || (ham.Contains('(') && ham.Contains(')'));

            foreach (var ch in ham)
            {
                if (char.IsDigit(ch) || ch == '.' || ch == ',')
                    temiz.Append(ch);
            }

            var s = temiz.ToString();
            if (s.Length == 0) return null;

            var sonNokta = s.LastIndexOf('.');
            var sonVirgul = s.LastIndexOf(',');

            string normalize;

            if (sonNokta >= 0 && sonVirgul >= 0)
            {
                
                if (sonVirgul > sonNokta)
                    normalize = s.Replace(".", "").Replace(',', '.');
                else
                    normalize = s.Replace(",", "");
            }
            else if (sonVirgul >= 0)
            {
           
                normalize = s.Replace(',', '.');
            }
            else if (sonNokta >= 0)
            {
                // Sadece nokta. Tek nokta + tam 3 hane sonrası → binlik ayracı ("1.234" = 1234)
                var noktaSayisi = s.Count(c => c == '.');
                var sonrasi = s.Length - sonNokta - 1;

                if (noktaSayisi > 1 || sonrasi == 3)
                    normalize = s.Replace(".", "");
                else
                    normalize = s;
            }
            else
            {
                normalize = s;
            }

            if (!decimal.TryParse(normalize, NumberStyles.Any, CultureInfo.InvariantCulture, out var sonuc))
                return null;

            return negatif && sonuc > 0 ? -sonuc : sonuc;
        }

        private static readonly string[] TarihBicimleri =
        {
            "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy",
            "yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd",
            "dd-MM-yyyy", "d-M-yyyy",
            "dd.MM.yyyy HH:mm", "dd/MM/yyyy HH:mm", "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy", "M/d/yyyy"
        };

        private static DateTime? TarihCevir(IXLCell? cell)
        {
            if (cell == null || cell.IsEmpty()) return null;

            // Hücre tarih biçimindeyse doğrudan al
            if (cell.TryGetValue<DateTime>(out var dt) && dt.Year > 1900 && dt.Year < 2200)
                return dt.Date;

            // Excel seri numarası
            if (cell.TryGetValue<double>(out var seri) && seri > 20000 && seri < 100000)
            {
                try { return DateTime.FromOADate(seri).Date; }
                catch { /* devam */ }
            }

            var ham = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(ham)) return null;

            var tr = CultureInfo.GetCultureInfo("tr-TR");

            if (DateTime.TryParseExact(ham, TarihBicimleri, tr, DateTimeStyles.None, out var kesin))
                return kesin.Date;

            if (DateTime.TryParseExact(ham, TarihBicimleri, CultureInfo.InvariantCulture, DateTimeStyles.None, out kesin))
                return kesin.Date;

            if (DateTime.TryParse(ham, tr, DateTimeStyles.None, out var serbest))
                return serbest.Date;

            if (DateTime.TryParse(ham, CultureInfo.InvariantCulture, DateTimeStyles.None, out serbest))
                return serbest.Date;

            return null;
        }

       
        public static string ReferansNormalizeEt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            var sb = new StringBuilder(s.Length);
            foreach (var ch in s.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            }

            var temiz = sb.ToString().TrimStart('0');
            return temiz.Length == 0 ? "0" : temiz;
        }

        private static string? Kirp(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static void UyariEkle(ExcelOkumaSonucu sonuc, string mesaj)
        {
            if (sonuc.Uyarilar.Count < MaksUyari)
                sonuc.Uyarilar.Add(mesaj);
            else if (sonuc.Uyarilar.Count == MaksUyari)
                sonuc.Uyarilar.Add("… daha fazla uyarı var, listelenmedi.");
        }

        private static string EnIcMesaj(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }
    }
}
