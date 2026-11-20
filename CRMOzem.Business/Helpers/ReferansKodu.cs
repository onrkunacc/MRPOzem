using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CRMOzem.Business.Helpers
{
    /// <summary>
    /// Referans (katalog) numarası ve ürün adı karşılaştırması için tek kural.
    ///
    /// Aynı ürün üç ayrı sistemde üç ayrı biçimde yazılabiliyor:
    ///   sipariş yazılımı : "07P60-30"
    ///   fiziki sayım     : "7P6030"
    ///   Tests.SutCode    : "07P6030 "
    /// Eski kural sadece baştaki sıfırı ve boşluğu atıyordu; tire, nokta,
    /// eğik çizgi farkı eşleşmeyi bozuyordu. Burada harf ve rakam dışındaki
    /// her şey atılır, baştaki sıfırlar silinir.
    /// </summary>
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
        /// Bir testin SutCode alanındaki BÜTÜN referanslar, normalize edilmiş.
        /// Aynı test birden çok referansla gelebilir (örn. 100'lük ve 2000'lik
        /// kit, ya da Architect ve Alinity kodu). Bunlar SutCode alanına virgül
        /// ya da noktalı virgülle ayrılarak yazılabilir: "8P4530, 8P4532".
        /// </summary>
        public static IEnumerable<string> Kodlar(string? sutCode)
        {
            if (string.IsNullOrWhiteSpace(sutCode)) yield break;

            foreach (var parca in sutCode.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var n = Normalize(parca);
                if (n.Length > 0) yield return n;
            }
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
