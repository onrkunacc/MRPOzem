using System.ComponentModel.DataAnnotations;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class TestCreateViewModel
    {
        [Required(ErrorMessage = "Test adı zorunludur.")]
        [StringLength(100)]
        public string TestName { get; set; }

        [StringLength(30)]
        public string? SutCode { get; set; }
        [StringLength(50)]
        public string? Kategori { get; set; }

        [StringLength(50)]
        public string? Platform { get; set; }

        [Display(Name = "Giriş Tipi")]
        public TestGirisTipi GirisTipi { get; set; } = TestGirisTipi.Adet;

        [Display(Name = "Kutu Başına Adet")]
        [Range(1, int.MaxValue, ErrorMessage = "Kutu başına adet 1'den büyük olmalı.")]
        public int? KutuBasinaAdet { get; set; }
    }
}