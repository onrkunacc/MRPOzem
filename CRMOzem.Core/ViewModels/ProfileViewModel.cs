using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        public string FullName { get; set; }
        [Required(ErrorMessage ="Kullanıcı adı zorunludur.")]
        public string UserName { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        public string Email { get; set; }  // Kullanıcı değiştiremez, sadece görüntüler

        [Phone(ErrorMessage = "Geçerli bir telefon numarası girin.")]
        public string PhoneNumber { get; set; }

        public string? Title { get; set; }  // Unvan opsiyonel
        public string? CardNumber { get; set; }
    }
}