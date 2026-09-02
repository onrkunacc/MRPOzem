using CRMOzem.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HospitalTestSetRowVm
    {
        public int TestID { get; set; }
        public string TestName { get; set; } = "";
        public string? SutCode { get; set; }
        // ── YENİ ──
        public string? Kategori { get; set; }
        public string? Platform { get; set; }
        public TestGirisTipi GirisTipi { get; set; }
        public int? KutuBasinaAdet { get; set; }

        public bool IsActive { get; set; }
        public bool IsLinked { get; set; }
    }
}
