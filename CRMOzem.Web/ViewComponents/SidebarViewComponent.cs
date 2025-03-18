using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class SidebarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SidebarViewComponent(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync((System.Security.Claims.ClaimsPrincipal)User);
        if (user == null)
        {
            return View(new List<string>()); // Kullanıcı yoksa boş liste döndür
        }

        var roles = await _userManager.GetRolesAsync(user);
        return View(roles);
    }
}
