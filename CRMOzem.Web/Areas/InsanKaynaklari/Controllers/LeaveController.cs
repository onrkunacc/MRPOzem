using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.InsanKaynaklari.Controllers
{
    [Area("InsanKaynaklari")]
    [Authorize(Roles = "İnsan Kaynakları,Yönetici")]
    public class LeaveController : Controller
    {
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaveController(ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _userManager = userManager;
        }
        public async Task<IActionResult> Index()
        {
            var leaves = await _leaveService.GetAllAsync(); 
            return View(leaves);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = _userManager.GetUserId(User);
            await _leaveService.ApproveLeaveAsync(id, userId);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var userId = _userManager.GetUserId(User);
            await _leaveService.RejectLeaveAsync(id, userId);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Detail(int id)
        {
            var leave = await _leaveService.GetByIdAsync(id);
            return View(leave);
        }
    }

}
