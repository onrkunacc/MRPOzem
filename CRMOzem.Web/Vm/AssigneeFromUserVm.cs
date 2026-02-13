using CRMOzem.Core.Models;

namespace CRMOzem.Web.Vm
{
    public sealed class AssigneeFromUserVm
    {
        public string? UserId { get; set; }
        public List<ApplicationUser> Users { get; set; } = new();
    }
}
