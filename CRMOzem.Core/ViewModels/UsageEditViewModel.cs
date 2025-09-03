using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class UsageEditViewModel
    {
        public int Id { get; set; }

        [Required]
        public int HospitalID { get; set; }

        [Required]
        public int TestID { get; set; }

        [Required]
        public DateTime Month { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Kullanım adedi negatif olamaz.")]
        public int TestCount { get; set; }
    }
}
