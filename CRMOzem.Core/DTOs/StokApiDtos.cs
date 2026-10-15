using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs
{
    public class StokApiResponseDto
    {
        public bool Success { get; set; }
        public int Count { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<StokApiItemDto> Items { get; set; } = new();
    }

    public class StokApiItemDto
    {
        public int StockId { get; set; }

        public int HospitalId { get; set; }
        public string HospitalName { get; set; } = "";

        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string ReferenceNumber { get; set; } = "";
        public string? ProductType { get; set; }
        public string? Platform { get; set; }

        public int BoxCount { get; set; }
        public int FlaconCount { get; set; }
        public int TestCount { get; set; }
        public int BoxTextCount { get; set; }
        public int FlaconCountPerBox { get; set; }

        public string? LotNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime LastUpdated { get; set; }

        public int SayimNo { get; set; }
        public bool SayimAktifMi { get; set; }
        public DateTime SayimTarihi { get; set; }
    }
}
