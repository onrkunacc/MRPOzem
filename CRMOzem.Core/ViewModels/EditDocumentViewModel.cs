using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class EditDocumentViewModel
    {
        public int Id { get; set; }

        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Konu alanı zorunludur.")]
        public string Subject { get; set; }

        [Required]
        public string DeliveryMethod { get; set; }

        public string? CargoCompany { get; set; }
    }
}
