using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    [Table("ResmiTatiller", Schema = "ozem")]
    public class ResmiTatil
    {
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime Tarih { get; set; }

        [Required, MaxLength(200)]
        public string Ad { get; set; } = "";

   
        public bool TamGun { get; set; } = true;

        [MaxLength(500)]
        public string? Aciklama { get; set; }
        public int Yil { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    }
}
