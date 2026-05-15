using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Core.ViewModels.Admin
{
    public class AdminExpenseChartsVm
    {
        public string? SelectedUserId { get; set; }
        public int? SelectedYear { get; set; }
        public List<SelectListItem> Users { get; set; } = new();
        public List<int> AvailableYears { get; set; } = new();

        // Aylık trend
        public List<MonthlyTrendPointVm> AylikTrend { get; set; } = new();

        // Kullanıcı karşılaştırması (yıllık)
        public List<UserComparisonVm> KullaniciKarsilastirma { get; set; } = new();

        // Kategori dağılımı
        public List<CategoryBreakdownVm> KategoriDagilim { get; set; } = new();

        // En yüksek 10 harcama
        public List<TopExpenseVm> EnYuksek10 { get; set; } = new();

        // Genel toplamlar
        public decimal YilToplam { get; set; }
        public decimal AylikOrtalama { get; set; }
    }

    public class MonthlyTrendPointVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = "";
        public decimal Toplam { get; set; }
    }

    public class UserComparisonVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public decimal Toplam { get; set; }
        public int IslemSayisi { get; set; }
    }

    public class TopExpenseVm
    {
        public int ReportId { get; set; }
        public string FullName { get; set; } = "";
        public string Kategori { get; set; } = "";
        public string Aciklama { get; set; } = "";
        public DateTime Tarih { get; set; }
        public decimal Tutar { get; set; }
    }
}