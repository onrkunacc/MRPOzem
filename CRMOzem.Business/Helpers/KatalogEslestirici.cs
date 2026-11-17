using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CRMOzem.Business.Helpers
{
    /// <summary>Bir ürünün katalogdaki (Testler) karşılığı nasıl bulundu?</summary>
    public enum KatalogEslesmeYontemi
    {
        Yok = 0,

        /// <summary>Referans numarası birebir aynı.</summary>
        Referans = 1,

        /// <summary>Referansın kökü aynı, yalnızca ambalaj (son iki hane) farklı.</summary>
        ReferansKoku = 2,

        /// <summary>Ürün adı aynı (ambalaj ve platform ekleri atıldıktan sonra).</summary>
        Ad = 3
    }

    /// <summary>Eşleştirmede kullanılan katalog satırı — Test tablosunun gereken alanları.</summary>
    public class KatalogSatiri
    {
        public int TestID { get; set; }
        public string TestName { get; set; } = "";
        public string? SutCode { get; set; }
        public string? Platform { get; set; }
    }


    public class KatalogEslestirici
    {
        private readonly Dictionary<string, int> _ref = new();
        private readonly Dictionary<string, HashSet<int>> _kok = new();
        private readonly Dictionary<string, List<KatalogSatiri>> _ad = new();

        public KatalogEslestirici(IEnumerable<KatalogSatiri> testler)
        {
            foreach (var t in testler.OrderBy(x => x.TestID)) Ekle(t);
        }

        /// <summary>Kataloğa sonradan eklenen bir testi aramaya dahil eder.</summary>
        public void Ekle(KatalogSatiri t)
        {
            var r = ReferansKodu.Normalize(t.SutCode);
            if (r.Length > 0)
            {
                _ref.TryAdd(r, t.TestID);

                var kok = Kok(r);
                if (kok != null)
                {
                    if (!_kok.TryGetValue(kok, out var set)) _kok[kok] = set = new HashSet<int>();
                    set.Add(t.TestID);
                }
            }

            var ad = AdCekirdek(t.TestName);
            if (ad.Length > 0)
            {
                if (!_ad.TryGetValue(ad, out var liste)) _ad[ad] = liste = new List<KatalogSatiri>();
                liste.Add(t);
            }
        }

        public (int? TestID, KatalogEslesmeYontemi Yontem) Bul(
            string? referans, string? urunAdi, string? platform, bool reaktif)
        {
            var r = ReferansKodu.Normalize(referans);

            // 1) Referans birebir — her ürün tipi için
            if (r.Length > 0 && _ref.TryGetValue(r, out var tid))
                return (tid, KatalogEslesmeYontemi.Referans);

            // Kök ve ad araması yalnızca reaktifler için: kalibratör, kontrol ve
            // sarf malzemesi adı benzese de bir teste bağlanmamalı.
            if (!reaktif) return (null, KatalogEslesmeYontemi.Yok);

            // 2) Referans kökü
            var kok = Kok(r);
            if (kok != null && _kok.TryGetValue(kok, out var adaylar) && adaylar.Count == 1)
                return (adaylar.First(), KatalogEslesmeYontemi.ReferansKoku);

            // 3) Ad çekirdeği
            var ad = AdCekirdek(urunAdi);
            if (ad.Length >= 2 && _ad.TryGetValue(ad, out var ayniAd))
            {
                if (ayniAd.Count == 1)
                    return (ayniAd[0].TestID, KatalogEslesmeYontemi.Ad);

                var p = PlatformAnahtar(platform);
                if (p.Length > 0)
                {
                    var uyan = ayniAd.Where(t => PlatformAnahtar(t.Platform) == p).ToList();
                    if (uyan.Count == 1)
                        return (uyan[0].TestID, KatalogEslesmeYontemi.Ad);
                }
            }

            return (null, KatalogEslesmeYontemi.Yok);
        }

        // ═══════════════════════════════════════════════════════════
        //  Yardımcılar
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Normalize edilmiş referansın kökü: son iki hane (ambalaj) atılır.
        /// "7K6534" → "7K65". Son iki hane rakam değilse, 20'den küçükse
        /// (kalibratör / kontrol uzantısı) ya da kök 4 karakterden kısaysa null.
        /// </summary>
        public static string? Kok(string normalizeReferans)
        {
            if (string.IsNullOrEmpty(normalizeReferans) || normalizeReferans.Length < 6) return null;

            var son = normalizeReferans.Substring(normalizeReferans.Length - 2);
            if (!char.IsDigit(son[0]) || !char.IsDigit(son[1])) return null;
            if (int.Parse(son, CultureInfo.InvariantCulture) < 20) return null;

            var kok = normalizeReferans.Substring(0, normalizeReferans.Length - 2);

            // Kök harf içermeli ("7K65", "8G63"); salt rakam kodlar Abbott biçimi değil
            return kok.Any(char.IsLetter) ? kok : null;
        }

        private static readonly Regex Parantez = new(@"\([^)]*\)|\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex CarpimEki = new(@"^\d+\s*x\s*\d+\s*t?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AdetEki = new(@"^(\d{3,6})\s*(t|test|tests)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> PlatformSozcukleri = new(StringComparer.OrdinalIgnoreCase)
        {
            "alinity", "architect", "abbott"
        };

        private static readonly HashSet<string> PlatformHarfleri = new(StringComparer.OrdinalIgnoreCase)
        {
            "i", "c", "ci", "ia", "cc"
        };

        /// <summary>
        /// Ürün adının karşılaştırılacak çekirdeği: parantez içi, " - 2000" gibi
        /// ambalaj ekleri, sondaki "4x100" / "1200" / "100T" ve baştaki
        /// "Alinity" / "Architect" atılır; kalan <see cref="ReferansKodu.AdNormalize"/> ile sadeleştirilir.
        ///
        ///   "FT4 - 2000"            → "ft4"
        ///   "CA 15-3 - 400(4x100)"  → "ca153"
        ///   "AFP (2 x 100T)"        → "afp"
        ///   "Alinity i TSH 1200"    → "tsh"
        ///   "CA 125"                → "ca125"   (125, 50'nin katı değil: ambalaj sayılmaz)
        ///   "25-OH Vitamin D"       → "25ohvitamind"
        /// </summary>
        public static string AdCekirdek(string? ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return "";

            var s = Parantez.Replace(ad, " ");

            // " - " ile ayrılan ek: yalnızca ek ambalaj bilgisiyse at
            var parcalar = s.Split(new[] { " - ", " – " }, StringSplitOptions.RemoveEmptyEntries);
            if (parcalar.Length > 1)
            {
                var kalan = parcalar.Take(1)
                    .Concat(parcalar.Skip(1).Where(p => !AmbalajEkiMi(p.Trim())))
                    .ToArray();
                s = string.Join(" ", kalan);
            }

            var sozcukler = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            // Baştaki platform adı ("Alinity i ...", "Architect ...")
            if (sozcukler.Count > 1 && PlatformSozcukleri.Contains(sozcukler[0]))
            {
                sozcukler.RemoveAt(0);
                if (sozcukler.Count > 1 && PlatformHarfleri.Contains(sozcukler[0]))
                    sozcukler.RemoveAt(0);
            }

            // Sondaki ambalaj ekleri ("1200", "4x100", "100T", "test")
            while (sozcukler.Count > 1 && AmbalajEkiMi(sozcukler[^1]))
                sozcukler.RemoveAt(sozcukler.Count - 1);

            return ReferansKodu.AdNormalize(string.Join(" ", sozcukler));
        }

        private static bool AmbalajEkiMi(string sozcuk)
        {
            if (sozcuk.Length == 0) return true;
            if (sozcuk.Equals("test", StringComparison.OrdinalIgnoreCase)
                || sozcuk.Equals("tests", StringComparison.OrdinalIgnoreCase)) return true;

            var yalin = sozcuk.Replace(".", "").Replace(",", "");
            if (CarpimEki.IsMatch(yalin)) return true;

            // Yalnızca 50'nin katı olan sayılar ambalajdır (100, 400, 1200, 2000).
            // "CA 125", "CA 199", "B12" gibi adın parçası olan sayılara dokunulmaz.
            var m = AdetEki.Match(yalin);
            return m.Success
                && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                && n >= 100 && n % 50 == 0;
        }

        private static string PlatformAnahtar(string? platform)
        {
            var p = (platform ?? "").ToLowerInvariant();
            if (p.Contains("alinity")) return "alinity";
            if (p.Contains("architect")) return "architect";
            return "";
        }
    }
}
