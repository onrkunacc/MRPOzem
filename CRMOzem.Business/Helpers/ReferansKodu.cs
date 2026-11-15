using System;
using System.Globalization;
using System.Text;

namespace CRMOzem.Business.Helpers
{

    public static class ReferansKodu
    {
        public static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToUpperInvariant(c));
            }

            var temiz = sb.ToString().TrimStart('0');
            if (temiz.Length == 0)
                return sb.Length > 0 ? "0" : "";

            return temiz;
        }

        /// <summary>
        /// Ürün adını karşılaştırmaya hazırlar: küçük harf, Türkçe karakterler
        /// sadeleştirilir, harf/rakam dışı her şey atılır.
        /// Yalnızca BİREBİR ad eşleşmesi için kullanılır — benzerlik değil.
        /// </summary>
        public static string AdNormalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            var kucuk = s.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
            var sb = new StringBuilder(kucuk.Length);

            foreach (var c in kucuk)
            {
                switch (c)
                {
                    case 'ı': sb.Append('i'); break;
                    case 'ğ': sb.Append('g'); break;
                    case 'ü': sb.Append('u'); break;
                    case 'ş': sb.Append('s'); break;
                    case 'ö': sb.Append('o'); break;
                    case 'ç': sb.Append('c'); break;
                    default:
                        if (char.IsLetterOrDigit(c)) sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
