using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CRMOzem.DataAccess.Seeders
{
    public class UserSeeder
    {
        public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // **📌 Kullanıcıları ve Rolleri Tanımla**
            var users = new List<(string UserName, string FullName, string Email, string PhoneNumber, string Title, string Role)>
            {
                ("admin", "Admin", "admin@ozemmedikal.com", "000-000-0000", "Yönetici", "Yönetici"),
                ("yonetici", "Yönetici", "yonetici@ozemmedikal.com", "111-111-1111", "Genel Müdür", "Yönetici"),
                ("muhasebe", "Muhasebe", "muhasebe@ozemmedikal.com", "222-222-2222", "Muhasebe Uzmanı", "Muhasebe"),
                ("finans", "Finans", "finans@ozemmedikal.com", "333-333-3333", "Finans Müdürü", "Finans"),
                ("ik", "İnsan Kaynakları", "ik@ozemmedikal.com", "444-444-4444", "İK Uzmanı", "İnsan Kaynakları"),
                ("ihale", "İhale", "ihale@ozemmedikal.com", "555-555-5555", "İhale Yetkilisi", "İhale"),
                ("evrak", "Evrak", "info@ozemmedikal.com", "555-555-5555", "Evrak İşlemleri", "Evrak"),

            };

            foreach (var (UserName, FullName, Email, PhoneNumber, Title, Role) in users)
            {
                if (userManager.Users.All(u => u.UserName != UserName))
                {
                    var user = new ApplicationUser
                    {
                        UserName = UserName,
                        FullName = FullName,
                        Email = Email,
                        PhoneNumber = PhoneNumber,
                        Title = Title,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(user, "ozem123");

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, Role);
                    }
                }
            }
        }
    }
}
