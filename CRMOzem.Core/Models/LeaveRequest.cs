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

        // ═══════════════ YENİ ALANLAR ═══════════════

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

        // ── Karar (onay / red) bilgileri ──

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
    }
}
