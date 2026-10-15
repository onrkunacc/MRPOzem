using CRMOzem.Core.Enums;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.DataAccess.Seeders
{
    public static class TedarikciSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();

            // Zaten kayıt varsa geç
            if (await db.Tedarikciler.AnyAsync()) return;

            var tedarikciler = new List<Tedarikci>
            {
                new()
                {
                    Ad = "Abbott",
                    Grup = TedarikciGrup.Abbott,
                    Aciklama = "Abbott Laboratories - Ana tedarikçi (USD faturalar)",
                    Aktif = true,
                    Sira = 1
                },
                new()
                {
                    Ad = "Özem",
                    Grup = TedarikciGrup.Ozem,
                    Aciklama = "Özem Tıbbi Cihazlar - Gelir faturaları",
                    Aktif = true,
                    Sira = 2
                }
            };

            await db.Tedarikciler.AddRangeAsync(tedarikciler);
            await db.SaveChangesAsync();
        }
    }
}
