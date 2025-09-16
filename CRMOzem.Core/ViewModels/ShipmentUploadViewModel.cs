using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class ShipmentUploadViewModel
    {
        [Required(ErrorMessage = "Lütfen bir dosya seçiniz.")]
        public IFormFile File { get; set; }

        [Required(ErrorMessage = "Lütfen bir hastane seçiniz.")]
        public int HospitalID { get; set; }

        [Required(ErrorMessage = "Lütfen ay seçiniz.")]
        [DataType(DataType.Date)]
        public DateTime Month { get; set; }

        public List<ShipmentPreviewItem> PreviewItems { get; set; } = new();
    }

    public class ShipmentPreviewItem
    {
        public int? TestID { get; set; }
        public string TestName { get; set; }
        public int SentCount { get; set; }

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
