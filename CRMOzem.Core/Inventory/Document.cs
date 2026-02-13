using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Inventory
{
    public sealed class Document
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public string FileKey { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "application/pdf";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
