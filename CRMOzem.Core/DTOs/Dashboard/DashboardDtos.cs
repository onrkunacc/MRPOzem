using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Dashboard
{
    public sealed class KpiDto
    {
        public long TotalShipment { get; set; }
        public long TotalUsage { get; set; }
        public int UniqueTests { get; set; }
        public int ActiveHospitals { get; set; }
    }

    public sealed class TrendPointDto
    {
        public string Month { get; set; } = default!; // "yyyy-MM"
        public long Shipment { get; set; }
        public long Usage { get; set; }
    }

    public sealed class TopItemDto
    {
        public string Name { get; set; } = default!;
        public long Value { get; set; }
    }

    public sealed class ByHospitalDto
    {
        public string Hospital { get; set; } = default!;
        public long Shipment { get; set; }
        public long Usage { get; set; }
    }
}
