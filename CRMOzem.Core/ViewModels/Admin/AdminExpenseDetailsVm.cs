namespace CRMOzem.Core.ViewModels.Admin
{
    public class AdminExpenseDetailsVm
    {
        public int ReportId { get; set; }
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public DateTime MonthYear { get; set; }

        // KPI
        public decimal Toplam { get; set; }
        public int IslemSayisi { get; set; }
        public decimal OrtalamaIslem { get; set; }
        public ExpenseDetailHighlightVm? EnYuksek { get; set; }

        // Kategori dağılımı
        public List<CategoryBreakdownVm> KategoriDagilim { get; set; } = new();

        // Tüm masraflar
        public List<ExpenseDetailRowVm> Detaylar { get; set; } = new();
    }

    public class ExpenseDetailHighlightVm
    {
        public int ExpenseId { get; set; }
        public string Aciklama { get; set; } = "";
        public string Kategori { get; set; } = "";
        public DateTime Tarih { get; set; }
        public decimal Tutar { get; set; }
    }

    public class CategoryBreakdownVm
    {
        public string Kategori { get; set; } = "";
        public decimal Toplam { get; set; }
        public int IslemSayisi { get; set; }
        public decimal Yuzde { get; set; }
    }

    public class ExpenseDetailRowVm
    {
        public int Id { get; set; }
        public DateTime Tarih { get; set; }
        public string Kategori { get; set; } = "";
        public string Aciklama { get; set; } = "";
        public string OdemeYontemi { get; set; } = "";
        public decimal Tutar { get; set; }

        // İlk 3 yüksek için badge
        public int? YuksekRank { get; set; }
    }
}