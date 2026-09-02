using System.ComponentModel.DataAnnotations;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class TestViewModel
    {
        public int TestID { get; set; }
        public string TestName { get; set; }

        [StringLength(30)]
        public string? SutCode { get; set; }
        [StringLength(50)]
        public string? Kategori { get; set; }

        [StringLength(50)]
        public string? Platform { get; set; }
        public TestGirisTipi GirisTipi { get; set; }
        public int? KutuBasinaAdet { get; set; }
    }
}