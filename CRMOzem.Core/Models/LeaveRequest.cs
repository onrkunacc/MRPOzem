using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Enums;

namespace CRMOzem.Core.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required]
        public int LeaveTypeId { get; set; }
        public LeaveType LeaveType { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public TimeSpan? StartHour { get; set; } // saatlik izin için
        public TimeSpan? EndHour { get; set; }

        public string? Reason { get; set; }

        public int StatusId { get; set; }

        [ForeignKey("StatusId")]
        public LeaveStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Talep anında hesaplanan iş günü sayısı. Dondurulur —
        /// sonradan resmi tatil tablosu değişse bile bu kayıt değişmez.
        /// </summary>
        public int TotalDays { get; set; }

        /// <summary>
        /// Bu talepte cumartesi iş gününden sayılmadı mı?
        /// Varsayılan false = cumartesi çalışılıyor, izinden düşer.
        /// </summary>
        public bool SaturdayExcluded { get; set; } = false;

        // ═══════════════════════════════════════════════════════════
        //  AŞAMA 1 — YÖNETİCİ (SÜPERVİZÖR) KARARI
        // ═══════════════════════════════════════════════════════════
        // Akış: Pending (yönetici onayında)
        //         → SupervisorApproved (İK onayında)
        //         → Approved / Rejected
        //
        // Çalışanın yöneticisi atanmamışsa 1. aşama atlanır: talep
        // doğrudan SupervisorApproved durumunda açılır ve
        // YoneticisizGecildi = true işaretlenir.

        /// <summary>
        /// Talep anındaki yönetici. Sonradan yönetici değişse bile
        /// kararı kimin vermesi gerektiği kayıtta sabit kalır.
        /// </summary>
        [MaxLength(450)]
        public string? SupervisorUserId { get; set; }

        /// <summary>Yöneticinin adı — anlık kopya, kullanıcı silinse de kayıt bozulmaz.</summary>
        [MaxLength(200)]
        public string? SupervisorName { get; set; }

        /// <summary>null = karar yok · true = onayladı · false = reddetti</summary>
        public bool? SupervisorOnayladi { get; set; }

        public DateTime? SupervisorDecisionAt { get; set; }

        /// <summary>Yöneticinin onay notu ya da red gerekçesi.</summary>
        [MaxLength(1000)]
        public string? SupervisorNote { get; set; }

        /// <summary>
        /// Çalışanın yöneticisi tanımlı olmadığı için 1. aşama atlandı mı?
        /// İK ekranında "yöneticisi yok" uyarısı bununla gösterilir.
        /// </summary>
        public bool YoneticisizGecildi { get; set; } = false;

        // ═══════════════════════════════════════════════════════════
        //  AŞAMA 2 — İK (NİHAİ) KARARI
        // ═══════════════════════════════════════════════════════════

        [MaxLength(450)]
        public string? DecisionByUserId { get; set; }

        /// <summary>
        /// Kararı veren kişinin adı — anlık kopya.
        /// Kullanıcı sonradan silinse/değişse bile kayıt bozulmaz.
        /// </summary>
        [MaxLength(200)]
        public string? DecisionByName { get; set; }

        public DateTime? DecisionAt { get; set; }

        /// <summary>
        /// Onay notu ya da red gerekçesi.
        /// </summary>
        [MaxLength(1000)]
        public string? DecisionNote { get; set; }

        // ═══════════════════════════════════════════════════════════
        //  HESAPLANAN (veritabanında tutulmaz)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Yönetici aşamasında bekliyor mu?</summary>
        [NotMapped]
        public bool YoneticiOnayindaMi => Status?.Code == "Pending";

        /// <summary>İK aşamasında bekliyor mu?</summary>
        [NotMapped]
        public bool IkOnayindaMi => Status?.Code == "SupervisorApproved";

        /// <summary>Hâlâ bir karar bekliyor mu (iki aşamadan biri)?</summary>
        [NotMapped]
        public bool AcikMi => YoneticiOnayindaMi || IkOnayindaMi;

        /// <summary>İlerleme göstergesi için 1 / 2 / 3 (3 = kapandı).</summary>
        [NotMapped]
        public int AsamaNo => Status?.Code switch
        {
            "Pending" => 1,
            "SupervisorApproved" => 2,
            _ => 3
        };

        /// <summary>Kararı şu an kimin vermesi gerekiyor?</summary>
        [NotMapped]
        public string SiradakiOnaylayan => Status?.Code switch
        {
            "Pending" => string.IsNullOrWhiteSpace(SupervisorName) ? "Yönetici" : SupervisorName!,
            "SupervisorApproved" => "İnsan Kaynakları",
            _ => "—"
        };

        /// <summary>Yönetici kararını tek satırda özetler.</summary>
        [NotMapped]
        public string YoneticiKararMetni
        {
            get
            {
                if (YoneticisizGecildi) return "Yöneticisi tanımlı değil — doğrudan İK onayına düştü";
                if (SupervisorOnayladi == true) return $"{SupervisorName ?? "Yönetici"} onayladı";
                if (SupervisorOnayladi == false) return $"{SupervisorName ?? "Yönetici"} reddetti";
                return $"{SupervisorName ?? "Yönetici"} onayı bekleniyor";
            }
        }
    }
}
