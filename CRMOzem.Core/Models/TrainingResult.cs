using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TrainingResult
    {
        public int Id { get; set; }
        public int TrainingId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }
}
