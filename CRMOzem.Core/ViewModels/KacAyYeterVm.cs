using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class KacAyYeterReportVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public DateTime ReportDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Dönem seçili değilse: "Geçen ay" etiketi
        /// Dönem seçiliyse: "01.01.2026 - 30.09.2026" gibi
        /// </summary>
        public string ReferenceMonthLabel { get; set; } = "";

        // ── DÖNEM FİLTRESİ (opsiyonel) ──
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>Dönem seçiliyse kaç ay olduğunu gösterir</summary>
        public int? PeriyotAySayisi { get; set; }

        /// <summary>Dönem seçili mi</summary>
        public bool IsPeriyotFiltreli => StartDate.HasValue && EndDate.HasValue;

        public List<KacAyYeterRowVm> Rows { get; set; } = new();

        // Özet KPI'lar
        public int ToplamTest => Rows.Count;
        public int KritikSayi => Rows.Count(r => r.Durum == StokDurum.Kritik);
        public int UyariSayi => Rows.Count(r => r.Durum == StokDurum.Uyari);
        public int IyiSayi => Rows.Count(r => r.Durum == StokDurum.Iyi);
        public int VeriYokSayi => Rows.Count(r => r.Durum == StokDurum.VeriYok);
    }

    public class KacAyYeterRowVm
    {
        public int TestID { get; set; }
        public string TestName { get; set; } = "";
        public string? SutCode { get; set; }
        public string? Kategori { get; set; }
        public string? Platform { get; set; }
        public TestGirisTipi GirisTipi { get; set; }
        public int? KutuBasinaAdet { get; set; }

        public int ToplamGonderilen { get; set; }
        public int ToplamKullanilan { get; set; }
        public int Stok => ToplamGonderilen - ToplamKullanilan;

        /// <summary>
        /// Dönem seçili değilse: geçen ayın tüketimi
        /// Dönem seçiliyse: dönem içindeki aylık ortalama tüketim (yuvarlanmış)
        /// </summary>
        public int GecenAyTuketim { get; set; }

        /// <summary>Kaç ay yeter — referans tüketime göre</summary>
        public decimal? KacAyYeter
        {
            get
            {
                if (GecenAyTuketim <= 0) return null;
                if (Stok <= 0) return 0;
                return Math.Round((decimal)Stok / GecenAyTuketim, 1);
            }
        }

        /// <summary>Kutu testler için stok'un kutu karşılığı</summary>
        public decimal? StokKutu
        {
            get
            {
                if (GirisTipi != TestGirisTipi.Kutu) return null;
                if (!KutuBasinaAdet.HasValue || KutuBasinaAdet.Value <= 0) return null;
                return Math.Round((decimal)Stok / KutuBasinaAdet.Value, 2);
            }
        }

        public StokDurum Durum
        {
            get
            {
                if (GecenAyTuketim <= 0 && Stok <= 0) return StokDurum.VeriYok;
                if (GecenAyTuketim <= 0) return StokDurum.VeriYok;

                var ay = (decimal)Stok / GecenAyTuketim;
                if (Stok <= 0) return StokDurum.Kritik;
                if (ay < 1) return StokDurum.Kritik;
                if (ay < 2) return StokDurum.Uyari;
                return StokDurum.Iyi;
            }
        }

        public string DurumText => Durum switch
        {
            StokDurum.Kritik => "Kritik",
            StokDurum.Uyari => "Uyarı",
            StokDurum.Iyi => "İyi",
            StokDurum.VeriYok => "Veri Yok",
            _ => "—"
        };
    }

    public enum StokDurum
    {
        VeriYok = 0,
        Kritik = 1,
        Uyari = 2,
        Iyi = 3
    }
}