using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TrainingAnswer
    {
        public int Id { get; set; }
        public int TrainingId { get; set; }
        public int QuestionId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string SelectedOption { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
