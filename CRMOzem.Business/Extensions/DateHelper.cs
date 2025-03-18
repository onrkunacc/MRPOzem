using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Extensions
{
    public static class DateHelper
    {
        public static string GetMonthName(int month)
        {
            var months = new[]
            {
                "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
            };

            if (month >= 1 && month <= 12)
            {
                return months[month - 1];
            }

            return "Bilinmiyor";
        }
    }
}
