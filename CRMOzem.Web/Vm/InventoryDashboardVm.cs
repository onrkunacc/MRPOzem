using CRMOzem.Core.Inventory;
using static CRMOzem.Web.Areas.IT.Controllers.InventoryController;

namespace CRMOzem.Web.Vm
{
    public sealed class InventoryDashboardVm
    {
        public List<Asset> Assets { get; set; } = new();
        public List<AssignmentItem> ActiveItems { get; set; } = new();
        public List<BrandSummaryRow> BrandSummary { get; set; } = new();
    }
}
