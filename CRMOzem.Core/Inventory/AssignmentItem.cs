using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Inventory
{
    public sealed class AssignmentItem
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public string? ConditionOut { get; set; }
        public string? ConditionIn { get; set; }
        public DateTime? ReturnedAtUtc { get; set; }
    }
}
