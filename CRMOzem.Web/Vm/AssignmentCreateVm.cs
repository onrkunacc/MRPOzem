using CRMOzem.Core.Inventory;

namespace CRMOzem.Web.Vm
{
    public sealed class AssignmentCreateVm
    {
        public List<Asset> AvailableAssets { get; set; } = new();
        public List<Assignee> RecentAssignees { get; set; } = new();
    }
}
