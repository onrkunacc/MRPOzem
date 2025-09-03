using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HospitalEditViewModel
    {
        public int HospitalID { get; set; }

        [Required(ErrorMessage = "Hastane adı zorunludur.")]
        [StringLength(150)]
        public string HospitalName { get; set; }
    }
}
