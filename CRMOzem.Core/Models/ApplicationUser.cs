using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Enums;
using Microsoft.AspNetCore.Identity;


namespace CRMOzem.Core.Models
{
    public class ApplicationUser : IdentityUser
    {

        public string FullName { get; set; }
        public string? Title { get; set; }
        public string? PhoneNumber { get; set; }
        public override string? Email { get; set; }
        public string? CardNumber { get; set; }
        public bool IsApproved { get; set; }
        [EnumDataType(typeof(PaymentMethod))]
        public PaymentMethod DefaultPaymentMethod { get; set; }
        public int BillingCutoffDay { get; set; } = 15;

        // ═══════════════ ÖZLÜK BİLGİLERİ (İK düzenler) ═══════════════

        /// <summary>İşe giriş tarihi — yıllık izin kıdem hesabının temeli.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "İşe Giriş Tarihi")]
        public DateTime? IseGirisTarihi { get; set; }

        /// <summary>Doğum tarihi — 18 yaş altı / 50 yaş üstü izin kuralı için.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Doğum Tarihi")]
        public DateTime? DogumTarihi { get; set; }

        /// <summary>TC kimlik numarası — izin belgesinde yazdırılır.</summary>
        [MaxLength(11)]
        [Display(Name = "TC Kimlik No")]
        public string? TcKimlikNo { get; set; }

        /// <summary>Departman / birim.</summary>
        [MaxLength(150)]
        [Display(Name = "Departman")]
        public string? Departman { get; set; }

        /// <summary>İşten çıkış tarihi — doluysa çalışan pasif sayılır.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "İşten Çıkış Tarihi")]
        public DateTime? IstenCikisTarihi { get; set; }

        // ── Hesaplanan (veritabanında tutulmaz) ──

        /// <summary>Hâlâ çalışıyor mu?</summary>
        public bool AktifCalisan => IstenCikisTarihi == null || IstenCikisTarihi.Value.Date > DateTime.Today;
    }
}
