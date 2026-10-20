using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class SaglikRaporuVm
    {
        public List<SaglikKontrolVm> Kontroller { get; set; } = new();
        public DateTime HesaplamaZamani { get; set; } = DateTime.Now;

        public int ToplamGecen => Kontroller.Count(k => k.Durum == KontrolDurumu.Gecti);
        public int ToplamDikkat => Kontroller.Count(k => k.Durum == KontrolDurumu.Dikkat);
        public int ToplamHata => Kontroller.Count(k => k.Durum == KontrolDurumu.Hata);
    }

    public enum KontrolDurumu
    {
        Gecti = 1,     // ✓ her şey yolunda
        Dikkat = 2,    // ⚠ bulgu var ama kritik değil
        Hata = 3       // ✗ sistem tutarsız — pilot öncesi düzeltilmeli
    }

    public class SaglikKontrolVm
    {
        public string Kod { get; set; } = "";          // "K01", "K02" vb.
        public string Kategori { get; set; } = "";     // "Veri bütünlüğü", "Hesaplama", "Mapping"
        public string Baslik { get; set; } = "";
        public string Aciklama { get; set; } = "";     // ne kontrol ediliyor

        public KontrolDurumu Durum { get; set; }
        public int BulguAdeti { get; set; }
        public List<string> BulguDetaylar { get; set; } = new();  // ilk 20 örnek

        public string? CozumOnerisi { get; set; }
        public string? AksiyonUrl { get; set; }
        public string? AksiyonText { get; set; }

        public long SureMs { get; set; }   // kontrolün ne kadar sürdüğü
    }
}
