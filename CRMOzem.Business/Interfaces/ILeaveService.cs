using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using CRMOzem.Core.ViewModels;

namespace CRMOzem.Business.Interfaces
{
    public interface ILeaveService
    {
        Task<List<LeaveRequest>> GetUserLeavesAsync(string userId);
        Task<int> GetRemainingLeaveDaysAsync(string userId);
        Task CreateLeaveAsync(LeaveRequest request);
        Task ApproveLeaveAsync(int leaveRequestId, string approverUserId);
        Task RejectLeaveAsync(int leaveRequestId, string approverUserId);
        Task CancelLeaveAsync(int leaveRequestId, string userId);
        Task<LeaveRequest> GetByIdAsync(int id);
        Task<List<LeaveRequest>> GetAllAsync();
        Task<List<LeaveType>> GetLeaveTypesAsync();

    }
}
