using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CRMOzem.Core.ViewModels
{
    public class FileUploadViewModel
    {
        [Required]
        public IFormFile File { get; set; }

        [Required]
        public string TargetPath { get; set; } // Örn: "2025/İhale/Proforma"
    }
}
