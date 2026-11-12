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
    public static class LeaveSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();

            await SeedStatusesAsync(db);
            await SeedTypesAsync(db);
        }

        // ═══════════════════════════════════════════════════════
        //  DURUMLAR
        // ═══════════════════════════════════════════════════════
        // Kodlar LeaveService içindeki sabitlerle birebir eşleşmeli.
        //
        // İKİ AŞAMALI AKIŞ:
        //   Pending            → çalışanın yöneticisi karar verecek  (1. aşama)
        //   SupervisorApproved → yönetici onayladı, İK karar verecek (2. aşama)
        //   Approved / Rejected / Cancelled → kapandı
        //
        // Yöneticisi atanmamış çalışanın talebi doğrudan
        // SupervisorApproved durumunda açılır (1. aşama atlanır).

        private static async Task SeedStatusesAsync(ApplicationDbContext db)
        {
            var hedef = new (string Code, string Name, string Aciklama)[]
            {
                ("Pending",            "Yönetici Onayında",
                    "Talep oluşturuldu, çalışanın yöneticisinin onayı bekleniyor."),

                ("SupervisorApproved", "İK Onayında",
                    "Yönetici onayladı, İnsan Kaynakları'nın nihai onayı bekleniyor."),

                ("Approved",           "Onaylandı",
                    "Talep onaylandı, izin bakiyesinden düşüldü."),

                ("Rejected",           "Reddedildi",
                    "Talep reddedildi, bakiyeden düşülmedi."),

                ("Cancelled",          "İptal Edildi",
                    "Talep çalışan tarafından iptal edildi.")
            };

            var mevcutlar = await db.LeaveStatuses.ToListAsync();

            var mevcutMap = mevcutlar
                .Where(s => s.Code != null)
                .GroupBy(s => s.Code!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var degisti = false;

            foreach (var h in hedef)
            {
                if (mevcutMap.TryGetValue(h.Code, out var kayit))
                {
                    // Ad ve açıklamayı hedefe hizala — "Bekliyor" artık
                    // "Yönetici Onayında" olmalı, yoksa ekranlar yanlış yazar.
                    if (kayit.Name != h.Name || kayit.Description != h.Aciklama)
                    {
                        kayit.Name = h.Name;
                        kayit.Description = h.Aciklama;
                        degisti = true;
                    }
                }
                else
                {
                    db.LeaveStatuses.Add(new LeaveStatus
                    {
                        Code = h.Code,
                        Name = h.Name,
                        Description = h.Aciklama
                    });
                    degisti = true;
                }
            }

            if (degisti)
                await db.SaveChangesAsync();
        }

        // ═══════════════════════════════════════════════════════
        //  İZİN TÜRLERİ
        // ═══════════════════════════════════════════════════════
        // IsDeductedFromQuota → yıllık izin hakkından düşer mi?
        // IsPaid              → ücretli mi?
        // IsHourly            → saatlik mi (gün yerine saat bazlı)?

        private static async Task SeedTypesAsync(ApplicationDbContext db)
        {
            var hedef = new (string Ad, bool Duser, bool Ucretli, bool Saatlik, string Aciklama)[]
            {
                ("Yıllık Ücretli İzin", true,  true,  false,
                    "4857 sayılı İş Kanunu m.53 kapsamında kullanılan yıllık izin. Hakedişten düşer."),

                ("Mazeret İzni", false, true,  false,
                    "Kanuni mazeret izinleri. Yıllık izin hakkından düşülmez."),

                ("Saatlik İzin", false, true,  true,
                    "Gün içinde kullanılan kısa süreli izin. Saat bazlı girilir."),

                ("Ücretsiz İzin", false, false, false,
                    "Ücret ödenmeyen izin. Yıllık izin hakkından düşülmez."),

                ("Hastalık / Rapor", false, false, false,
                    "Sağlık raporuna dayalı istirahat. Ödeme SGK tarafından yapılır."),

                ("Evlilik İzni", false, true,  false,
                    "İş Kanunu Ek m.2 — evlenme halinde 3 gün ücretli izin."),

                ("Doğum İzni", false, true,  false,
                    "Analık izni. Doğumdan önce 8, sonra 8 hafta."),

                ("Babalık İzni", false, true,  false,
                    "İş Kanunu Ek m.2 — eşin doğum yapması halinde 5 gün ücretli izin."),

                ("Ölüm İzni", false, true,  false,
                    "İş Kanunu Ek m.2 — birinci derece yakının vefatında 3 gün ücretli izin."),

                ("Refakat İzni", false, true,  false,
                    "Yakınının tedavisine refakat için verilen izin."),

                ("İdari İzin", false, true,  false,
                    "İşveren tarafından verilen ücretli idari izin.")
            };

            var mevcut = await db.LeaveTypes
                .Select(t => t.Name)
                .ToListAsync();

            var mevcutSet = mevcut
                .Where(n => n != null)
                .Select(n => n!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var eklenecek = hedef
                .Where(h => !mevcutSet.Contains(h.Ad))
                .Select(h => new LeaveType
                {
                    Name = h.Ad,
                    Description = h.Aciklama,
                    IsDeductedFromQuota = h.Duser,
                    IsPaid = h.Ucretli,
                    IsHourly = h.Saatlik
                })
                .ToList();

            if (eklenecek.Count > 0)
            {
                db.LeaveTypes.AddRange(eklenecek);
                await db.SaveChangesAsync();
            }
        }
    }
}