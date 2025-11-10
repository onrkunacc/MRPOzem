using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class Training
    {
        public int Id { get; set; } // eğitim kimliği


        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty; // eğitim başlığı


        [MaxLength(500)]
        public string? Description { get; set; } // eğitim açıklaması 


        [Required]
        public DateTime TrainingDate { get; set; } // eğitim tarihi


        [Required, Url]
        public string Link { get; set; } = string.Empty; // eğitim linki


        public bool IsPublished { get; set; } = false; // yayında mı


        public string CreatedBy { get; set; } = string.Empty; // eğitimi oluşturan kullanıcı
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // oluşturulma tarihi


        public ICollection<TrainingParticipation> Participations { get; set; } = new List<TrainingParticipation>(); // katılımlar
        public ICollection<TrainingQuestion>? Questions { get; set; } // eğitim soruları

    }
}
