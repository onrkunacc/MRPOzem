using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class UserSupervisor
    {
        public int Id { get; set; }

        /// <summary>Astın (çalışanın) kullanıcı id'si.</summary>
        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        /// <summary>Yöneticinin kullanıcı id'si.</summary>
        [Required]
        public string SupervisorId { get; set; }
        public ApplicationUser Supervisor { get; set; }

        // ── Denetim izi (kim, ne zaman atadı) ──

        public DateTime AtandiTarih { get; set; } = DateTime.Now;

        [MaxLength(450)]
        public string? AtayanUserId { get; set; }

        [MaxLength(200)]
        public string? AtayanAd { get; set; }
    }

}
