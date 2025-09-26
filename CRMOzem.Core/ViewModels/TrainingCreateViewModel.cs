using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class TrainingCreateViewModel
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;


        [MaxLength(500)]
        public string? Description { get; set; }


        [Required]
        public DateTime TrainingDate { get; set; } = DateTime.Now;


        [Required, Url]
        public string Link { get; set; } = string.Empty;


        public bool IsPublished { get; set; } = false;
    }
}
