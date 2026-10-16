using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs
{
    public class SiparisApiResponseDto
    {
        public bool Success { get; set; }
        public int Count { get; set; }
        public string? GeneratedAt { get; set; }   // string olarak gelir, parse edilir
        public List<SiparisApiOrderDto> Orders { get; set; } = new();
    }

    public class SiparisApiOrderDto
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerEmail { get; set; }
        public string? HospitalName { get; set; }  // → CRMOzem.Hospital.HospitalName ile normalize ederek eşleşir
        public string? OrderDate { get; set; }     // "2026-10-14 10:35:15" format
        public int Status { get; set; }
        public string? StatusText { get; set; }
        public string? Note { get; set; }
        public List<SiparisApiItemDto> Items { get; set; } = new();
    }

    public class SiparisApiItemDto
    {
        public int ItemId { get; set; }
        public int ProductId { get; set; }         // PHP tarafındaki urunler.id
        public string? ProductName { get; set; }
        public string? ReferenceNumber { get; set; } // → normalize ederek FizikiSayimSnapshot.ReferenceNumber ile eşleşir
        public string? ProductDetay { get; set; }
        public string? Paketleme { get; set; }
        public int AmbalajId { get; set; }
        public string? AmbalajAdi { get; set; }    // "Kutu", "Flakon", vb.
        public int Miktar { get; set; }            // kullanıcı girişi (kutu/flakon cinsinden)
    }
}
