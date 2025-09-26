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
        public int Id { get; set; }


        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;


        [MaxLength(500)]
        public string? Description { get; set; }


        [Required]
        public DateTime TrainingDate { get; set; }


        [Required, Url]
        public string Link { get; set; } = string.Empty; // Google Drive linki


        public bool IsPublished { get; set; } = false;


        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public ICollection<TrainingParticipation> Participations { get; set; } = new List<TrainingParticipation>();
    }
}
