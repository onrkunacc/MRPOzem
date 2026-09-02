using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

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
        public DateTime Month { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        public List<StatisticsPreviewItem> PreviewItems { get; set; } = new();
    }

    public class StatisticsPreviewItem
    {
        public int? TestID { get; set; }
        public string? SutCode { get; set; }
        public string TestName { get; set; } = "";
        public int TestCount { get; set; }

        /// <summary>Eşleşme kaynağı: "sut", "name", "none"</summary>
        public string MatchSource { get; set; } = "none";

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}