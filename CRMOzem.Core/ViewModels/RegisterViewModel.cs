using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Ad-Soyad gereklidir.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
        [RegularExpression(@"^[a-zA-Z0-9._-]{3,20}$", ErrorMessage = "Kullanıcı adı yalnızca harf, rakam ve ._- içerebilir.")]
        public string UserName { get; set; } = string.Empty; 
        public string Title { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Telefon numarası gereklidir.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Şifre gereklidir.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Şifre tekrar gereklidir.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Şifreler uyuşmuyor.")]
        public string ConfirmPassword { get; set; }

    }
}
