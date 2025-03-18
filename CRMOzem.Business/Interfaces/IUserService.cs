using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Identity;


namespace CRMOzem.Business.Interfaces
{
    public interface IUserService
    {
        Task<SignInResult> LoginUserAsync(LoginViewModel model);
        Task<IdentityResult> RegisterUserAsync(RegisterViewModel model);
        Task LogoutUserAsync();
        Task<List<ApplicationUser>> GetUsersByRoleAsync(string roleName);
    }
}
