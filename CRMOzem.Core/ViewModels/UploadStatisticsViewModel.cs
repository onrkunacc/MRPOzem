using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class UploadStatisticsViewModel
    {
        [Required(ErrorMessage = "Lütfen bir dosya seçiniz.")]
        public IFormFile File { get; set; }

        [Required(ErrorMessage = "Lütfen bir hastane seçiniz.")]
        public int HospitalID { get; set; }

        [Required(ErrorMessage = "Lütfen ay seçiniz.")]
        [DataType(DataType.Date)]
        public DateTime Month { get; set; }

        public List<StatisticsPreviewItem> PreviewItems { get; set; } = new();
    }

    public class StatisticsPreviewItem
    {
        public int? TestID { get; set; }
        public string TestName { get; set; }
        public int TestCount { get; set; }

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
