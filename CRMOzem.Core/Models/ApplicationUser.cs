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

    }
}
