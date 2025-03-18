using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRMOzem.Core.Models;
using System.Linq;
using System.Threading.Tasks;

public class ModuleViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ModuleViewComponent(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
        var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

        return View(roles);
    }
}
