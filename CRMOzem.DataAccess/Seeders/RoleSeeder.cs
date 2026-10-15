using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CRMOzem.DataAccess.Seeders
{
    public class RoleSeeder
    {
        public static async Task SeedRoleAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            string[] roleNames = {
                "İnsan Kaynakları",
                "Finans",
                "İhale",
                "Yönetici",
                "Kullanıcı",
                "Muhasebe",
                "Evrak",
                "İstatistik",
                "IT",
                "Aplikasyon",
                "Lojistik",
                "İstatistikGiris",
                "KotaTakip"
            };

            foreach (var role in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}
