using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IResmiTatilService
    {
        Task<List<ResmiTatil>> GetByYearAsync(int yil);

        Task<ResmiTatil?> GetByIdAsync(int id);

        /// <summary>Gün hesabında kullanılacak tam gün tatiller.</summary>
        Task<List<DateTime>> GetTamGunTatillerAsync(DateTime baslangic, DateTime bitis);

        /// <summary>
        /// Tarihi her yıl sabit olan milli bayramları toplu ekler.
        /// Dini bayramlar her yıl kaydığı için buraya dahil değildir — İK elle girer.
        /// Zaten kayıtlı olan tarihler atlanır.
        /// </summary>
        Task<(int Eklenen, int Atlanan)> SabitTatilleriOlusturAsync(int yil);

        Task<(bool Basarili, string? Hata)> EkleAsync(ResmiTatil tatil);

        /// <summary>Birden fazla günü tek seferde ekler — bayram blokları için.</summary>
        Task<(int Eklenen, int Atlanan, string? Hata)> AralikEkleAsync(
            DateTime baslangic, DateTime bitis, string ad, bool tamGun, string? aciklama);

        Task<(bool Basarili, string? Hata)> GuncelleAsync(ResmiTatil tatil);

        Task<(bool Basarili, string? Hata)> SilAsync(int id);

        Task<List<int>> GetAvailableYearsAsync();
    }
}
