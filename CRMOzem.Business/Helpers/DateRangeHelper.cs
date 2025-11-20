using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Helpers
{
    public static class DateRangeHelper
    {
        public static DateTime NormalizeStart(DateTime? d)
            => (d?.Date) ?? new DateTime(DateTime.UtcNow.Year, 1, 1);

        public static DateTime NormalizeEnd(DateTime? d)
        {
            var x = (d?.Date) ?? DateTime.UtcNow.Date;
            return x.AddDays(1).AddTicks(-1); // gün sonu
        }
    }
}
