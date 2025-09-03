using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class ShipmentViewModel
    {
        [Required(ErrorMessage = "Hastane seçimi zorunludur.")]
        public int HospitalID { get; set; }

        [Required(ErrorMessage = "Test seçimi zorunludur.")]
        public int TestID { get; set; }

        [Required(ErrorMessage = "Gönderim tarihi zorunludur.")]
        [DataType(DataType.Date)]
        public DateTime Month { get; set; }

        [Required(ErrorMessage = "Gönderim adedi zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Gönderim adedi 0 veya negatif olamaz.")]
        public int SentCount { get; set; }
    }
}
