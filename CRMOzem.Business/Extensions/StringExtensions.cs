using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Extensions
{
    public static class StringExtensions
    {
        public static string FormatAsPhone(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length != 10)
                return input;

            return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)} {digits.Substring(6, 2)} {digits.Substring(8, 2)}";
        }
    }
}
