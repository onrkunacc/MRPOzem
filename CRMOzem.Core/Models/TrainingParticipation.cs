using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TrainingParticipation
    {
        public int Id { get; set; }


        [Required]
        public int TrainingId { get; set; }
        public Training Training { get; set; } = null!;


        [Required]
        public string UserId { get; set; } = string.Empty;


        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;


        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
    }
}
