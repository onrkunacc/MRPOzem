using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Inventory
{
    public sealed class Assignee
    {
        public int Id { get; set; }

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public string FullName { get; set; } = "";
        public string? Title { get; set; }
        public string? Department { get; set; }
        public string? TcNo { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
