using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Inventory
{
    public sealed class Assignment
    {
        public int Id { get; set; }

        public int AssigneeId { get; set; }
        public Assignee? Assignee { get; set; }

        public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        public List<AssignmentItem> Items { get; set; } = new();
    }
}
