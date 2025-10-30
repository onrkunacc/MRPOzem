using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TrainingVisibleUser
    {
        public int Id { get; set; }

        public int TrainingId { get; set; }
        public string UserId { get; set; } = string.Empty;

        // Navigation props
        public Training Training { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
    }
}
