using CRMOzem.Business.Interfaces;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class SidebarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILeaveService _leaveService;

    public SidebarViewComponent(
        UserManager<ApplicationUser> userManager,
        ILeaveService leaveService)
    {
        _userManager = userManager;
        _leaveService = leaveService;
    }

  
    public const string SANAL_ROL_YONETICI = "__Süpervizör";

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync((System.Security.Claims.ClaimsPrincipal)User);
        if (user == null)
        {
            return View(new List<string>()); // Kullanıcı yoksa boş liste döndür
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        // Astı olan kullanıcıya ekip menüsünü aç
        try
        {
            if (await _leaveService.YoneticiMiAsync(user.Id))
                roles.Add(SANAL_ROL_YONETICI);
        }
        catch
        {
            // Menü çizimi veritabanı hatasına takılmasın — kalem görünmez, o kadar
        }

        return View(roles);
    }
}