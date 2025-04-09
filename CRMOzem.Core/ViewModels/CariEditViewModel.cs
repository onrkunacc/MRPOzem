using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Core.ViewModels
{
    public class CariEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Cari Kodu zorunludur.")]
        public string CariKodu { get; set; }

        [Required(ErrorMessage = "Unvan zorunludur.")]
        public string Unvan { get; set; }

        public bool Tevkifat { get; set; }

        [Required(ErrorMessage = "İl zorunludur.")]
        public string Il { get; set; }

        [Required(ErrorMessage = "İlçe zorunludur.")]
        public string Ilce { get; set; }

        public string? TeklifNo { get; set; }     
        public string? IhaleNo { get; set; }      

        [Required(ErrorMessage = "Cari Grubu seçmelisiniz.")]
        public int CariGrubuId { get; set; }

        // ✅ Radio button için IEnumerable<SelectListItem>
        public IEnumerable<SelectListItem> CariGruplari { get; set; }
    }
}
