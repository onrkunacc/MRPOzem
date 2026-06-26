using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class HospitalListVm
    {
        public List<HospitalRowVm> Hospitals { get; set; } = new();
        public HospitalListSummaryVm Summary { get; set; } = new();
    }

    public class HospitalRowVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public string? Address { get; set; }
        public string? Phone { get; set; }

        public int AktifBirimSayisi { get; set; }
        public int ToplamBirimKaydi { get; set; }
        public List<string> AktifBirimAdlari { get; set; } = new();

        public int TestTemplateSayisi { get; set; }
        public DateTime? SonVeriTarihi { get; set; }
    }

    public class HospitalListSummaryVm
    {
        public int ToplamHastane { get; set; }
        public int AktifBirimToplam { get; set; }
        public int VerisiOlanHastane { get; set; }
        public int TestTanimliHastane { get; set; }
    }
}