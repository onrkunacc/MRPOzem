using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.DTOs.Istatistik
{
    public class SutComparisonRowVm
    {
        public int Sira { get; set; }
        public string MalzemeninAdi { get; set; }
        public string SutKodu { get; set; }

        public int TetkikSayisi { get; set; }

        public decimal SutPuaniIhale { get; set; }
        public decimal GuncelSut { get; set; }
        public decimal SutFark => GuncelSut - SutPuaniIhale;

        public decimal IhalePuan => TetkikSayisi * SutPuaniIhale;
        public decimal FarkPuan => TetkikSayisi * SutFark;
    }
}
