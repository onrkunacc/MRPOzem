using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class FollowDocument
    {
        public int Id { get; set; }
        public string Code { get; set; }

        [Required]
        public string Subject { get; set; }

        public DateTime Date { get; set; }

        [Required]
        public int CategoryId { get; set; }
        public DocumentCategory Category { get; set; }

        // Gönderim Biçimi (Elden veya Kargo)
        public string DeliveryMethod { get; set; } 

        // Eğer Kargo ise hangi firma?
        public string? CargoCompany { get; set; } 

        // Evrak gitti mi?
        public bool IsSent { get; set; } = false;

        // İşaretlendiği Tarih
        public DateTime? SentDate { get; set; }

        // Evrak Notları
        public string? Notes { get; set; }
    }
}
