using CRMOzem.Core.DTOs;
using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMOzem.Business.Helpers
{

    public class SiparisMusteriCozucu
    {
        private readonly Dictionary<string, HospitalEslesmesi> _map;
        private readonly Dictionary<string, HashSet<int>> _kodKullanicilari;

        public SiparisMusteriCozucu(IEnumerable<HospitalEslesmesi> eslesmeler, IEnumerable<SiparisApiOrderDto> siparisler)
        {
            _map = eslesmeler
                .GroupBy(e => (e.ExternalMusteriKodu ?? "").Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            _kodKullanicilari = siparisler
                .GroupBy(o => Kod(o.CustomerCode))
                .ToDictionary(g => g.Key, g => g.Select(o => o.CustomerId).Where(i => i > 0).ToHashSet());
        }

        public static string Kod(string? musteriKodu) => (musteriKodu ?? "").Trim().ToUpperInvariant();

        /// <summary>Eşleştirme anahtarı: "KOD/kullanıcı no". Kullanıcı no yoksa yalnızca kod.</summary>
        public static string Anahtar(string? musteriKodu, int customerId)
            => customerId > 0 ? $"{Kod(musteriKodu)}/{customerId}" : Kod(musteriKodu);

        public static string Anahtar(SiparisApiOrderDto o) => Anahtar(o.CustomerCode, o.CustomerId);

        /// <summary>Bu kod birden çok kurum (kullanıcı) tarafından mı kullanılıyor?</summary>
        public bool OrtakKod(string? musteriKodu)
            => _kodKullanicilari.TryGetValue(Kod(musteriKodu), out var k) && k.Count > 1;

        /// <summary>Kodu birden çok kurumda ortak olan müşteri kodları.</summary>
        public IReadOnlyCollection<string> OrtakKodlar
            => _kodKullanicilari.Where(x => x.Value.Count > 1).Select(x => x.Key).ToList();

        /// <summary>Siparişin eşleştirmesi; bağlı değilse null.</summary>
        public HospitalEslesmesi? Bul(SiparisApiOrderDto o)
        {
            if (_map.TryGetValue(Anahtar(o), out var kuruma)) return kuruma;

            // Eski, yalnızca kodla yapılmış eşleştirme — kod tek kuruma aitse geçerli
            var kod = Kod(o.CustomerCode);
            if (!OrtakKod(kod) && _map.TryGetValue(kod, out var koda)) return koda;

            return null;
        }

        /// <summary>
        /// Tablo satırı yalnızca kodla yapılmış ve kod artık ortak mı?
        /// (Bu satır hiçbir siparişe uygulanmıyor demektir.)
        /// </summary>
        public bool GecersizEskiEslesme(HospitalEslesmesi e)
        {
            var anahtar = (e.ExternalMusteriKodu ?? "").Trim().ToUpperInvariant();
            return !anahtar.Contains('/') && OrtakKod(anahtar);
        }
    }
}
