namespace CRMOzem.Web.Vm
{
    public sealed class AssignmentCreatePostVm
    {
        public int AssigneeId { get; set; }
        public List<int> AssetIds { get; set; } = new();
        public string? Notes { get; set; }
        public string? ConditionOut { get; set; }
    }
}
