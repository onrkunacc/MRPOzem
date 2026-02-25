using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class SutComparisonVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; }

        public int IhaleYear { get; set; }
        public int CurrentYear { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }

        public List<SutComparisonRowVm> Rows { get; set; } = new();
        public decimal TotalIhalePuan => Rows.Sum(x => x.IhalePuan);
        public decimal TotalFarkPuan => Rows.Sum(x => x.FarkPuan);
    }
}
