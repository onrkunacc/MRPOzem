namespace CRMOzem.Core.DTOs.Istatistik
{
    public class HospitalIstatistikDashboardVm
    {
        public int Yil { get; set; }
        public List<int> SeciliAylar { get; set; } = new();
        public List<HospitalIstatistikRowVm> Hospitaller { get; set; } = new();
    }

    public class HospitalIstatistikRowVm
    {
        public int HospitalID { get; set; }
        public string HospitalAdi { get; set; } = "";
        public DateTime? IhaleTarihi { get; set; }
        public string? IhaleNumarasi { get; set; }
        public List<AyDurumVm> Aylar { get; set; } = new();
    }

    // Mevcut AyDurumVm kullanılıyor (aynı dosyada veya başka), TEKRAR TANIMLAMAYACAĞIZ.
    // Sadece request modellerini ekliyoruz:

    public class SetHospitalGeldiMiRequest
    {
        public int HospitalID { get; set; }
        public int Yil { get; set; }
        public int Ay { get; set; }
        public bool GeldiMi { get; set; }
    }

    public class SetHospitalNotRequest
    {
        public int HospitalID { get; set; }
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string? Not { get; set; }
    }
}