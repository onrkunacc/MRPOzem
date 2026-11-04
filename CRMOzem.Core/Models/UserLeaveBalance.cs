using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.Models
{
  
    public class UserLeaveBalance
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int Year { get; set; }

      
        public int TotalDays { get; set; }

    
        public int UsedDays { get; set; }

      
        public int CarriedOverDays { get; set; }

        
        public bool ManuelGirildi { get; set; }

       
        public int? KidemYili { get; set; }

        [MaxLength(500)]
        public string? Aciklama { get; set; }
    }
}