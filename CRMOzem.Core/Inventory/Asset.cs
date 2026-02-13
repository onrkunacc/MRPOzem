using CRMOzem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Inventory
{
    public sealed class Asset
    {
        public int Id { get; set; }
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public string? SerialNumber { get; set; }
        public string? InventoryTag { get; set; }
        public string? ImeiNo { get; set; }
        public string? BarcodeNo { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Available;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
