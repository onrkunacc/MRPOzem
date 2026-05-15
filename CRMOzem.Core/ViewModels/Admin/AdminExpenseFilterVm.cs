using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Core.ViewModels.Admin
{
    public class AdminExpenseIndexVm
    {
        
        public AdminExpenseFilterVm Filter { get; set; } = new();


        public ExpenseSummaryVm Summary { get; set; } = new();

        
        public X.PagedList.IPagedList<ExpenseReportRowVm>? PagedReports { get; set; }

        
        public List<SelectListItem> Users { get; set; } = new();
        public List<int> AvailableYears { get; set; } = new();
        public List<string> AvailableCategories { get; set; } = new();
    }

    public class AdminExpenseFilterVm
    {
        public string? UserId { get; set; }
        public int? Year { get; set; }
        public int? Month { get; set; }
        public string? Category { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public string SortColumn { get; set; } = "MonthYear";
        public string SortDirection { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public string ViewMode { get; set; } = "table"; // table | card
    }

    public class ExpenseSummaryVm
    {
        public decimal SeciliDonemToplam { get; set; }
        public decimal YilToplam { get; set; }
        public int SeciliDonemIslemSayisi { get; set; }
        public decimal OrtalamaIslemTutari { get; set; }

        public TopUserMiniVm? EnYuksekHarcayan { get; set; }
        public ExpenseHighlightVm? EnYuksekTekHarcama { get; set; }
    }

    public class TopUserMiniVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public decimal Toplam { get; set; }
    }

    public class ExpenseHighlightVm
    {
        public int ReportId { get; set; }
        public string Aciklama { get; set; } = "";
        public string Kategori { get; set; } = "";
        public string FullName { get; set; } = "";
        public DateTime Tarih { get; set; }
        public decimal Tutar { get; set; }
    }

    public class ExpenseReportRowVm
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public DateTime MonthYear { get; set; }
        public decimal TotalAmount { get; set; }
        public int IslemSayisi { get; set; }

        // Mini sparkline için son 6 ayın değerleri
        public List<decimal> Sparkline { get; set; } = new();
    }
}