using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRMOzem.Web.Controllers
{
    [Authorize]
    public class LeaveRequestController : Controller
    {
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaveRequestController(ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> MyLeaves()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var model = new LeaveListViewModel
                {
                    Leaves = await _leaveService.GetUserLeavesAsync(userId),
                    RemainingDays = await _leaveService.GetRemainingLeaveDaysAsync(userId)
                };

                return View("MyLeaves", model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "İzin listesi yüklenirken hata oluştu.";
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            try
            {
                var leaveTypes = await _leaveService.GetLeaveTypesAsync();

                var model = new CreateLeaveRequestViewModel
                {
                    LeaveTypes = leaveTypes.Select(x => new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = x.Name
                    }).ToList()
                };

                return View(model);
            }
            catch
            {
                TempData["Error"] = "İzin türleri yüklenemedi.";
                return RedirectToAction("MyLeaves");
            }
        }


        [HttpPost]
        public async Task<IActionResult> Create(CreateLeaveRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen tüm alanları doldurun.";
                return View(model);
            }

            try
            {
                var userId = _userManager.GetUserId(User);

                var request = new LeaveRequest
                {
                    UserId = userId,
                    LeaveTypeId = model.LeaveTypeId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Reason = model.Reason
                };

                await _leaveService.CreateLeaveAsync(request);
                TempData["Success"] = "İzin talebiniz gönderildi.";
                return RedirectToAction("MyLeaves");
            }
            catch
            {
                TempData["Error"] = "İzin talebi gönderilemedi.";
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                await _leaveService.CancelLeaveAsync(id, userId);
                TempData["Success"] = "İzin talebiniz iptal edildi.";
            }
            catch
            {
                TempData["Error"] = "İzin iptali başarısız oldu.";
            }

            return RedirectToAction("MyLeaves");
        }
    }

}
