using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.Business.Services
{
    public class LeaveService
    {
        private readonly ApplicationDbContext _context;

        public LeaveService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<LeaveRequest>> GetUserLeavesAsync(string userId)
        {
            return await _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Include(l => l.Status)
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetRemainingLeaveDaysAsync(string userId)
        {
            var year = DateTime.Now.Year;

            var balance = await _context.UserLeaveBalances
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == year);

            if (balance == null)
                return 0;

            return balance.TotalDays - balance.UsedDays;
        }

        public async Task CreateLeaveAsync(LeaveRequest request)
        {
            request.CreatedAt = DateTime.Now;

            var leaveType = await _context.LeaveTypes.FindAsync(request.LeaveTypeId);

            // Hak düşüyorsa gün hesapla ve güncel bakiyeyi güncelle
            if (leaveType != null && leaveType.IsDeductedFromQuota)
            {
                var days = (request.EndDate - request.StartDate).Days + 1;

                var year = DateTime.Now.Year;
                var balance = await _context.UserLeaveBalances
                    .FirstOrDefaultAsync(b => b.UserId == request.UserId && b.Year == year);

                if (balance != null)
                {
                    balance.UsedDays += days;
                }
            }

            await _context.LeaveRequests.AddAsync(request);
            await _context.SaveChangesAsync();
        }

        public async Task ApproveLeaveAsync(int leaveRequestId, string approverUserId)
        {
            var request = await _context.LeaveRequests.FindAsync(leaveRequestId);

            if (request == null)
                throw new Exception("İzin talebi bulunamadı");

            var statusApproved = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == "Approved");

            request.StatusId = statusApproved.Id;

            await _context.SaveChangesAsync();
        }

        public async Task RejectLeaveAsync(int leaveRequestId, string approverUserId)
        {
            var request = await _context.LeaveRequests.FindAsync(leaveRequestId);

            if (request == null)
                throw new Exception("İzin talebi bulunamadı");

            var statusRejected = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == "Rejected");

            request.StatusId = statusRejected.Id;

            await _context.SaveChangesAsync();
        }

        public async Task CancelLeaveAsync(int leaveRequestId, string userId)
        {
            var request = await _context.LeaveRequests.FindAsync(leaveRequestId);

            if (request == null || request.UserId != userId)
                throw new Exception("İptal işlemi yapılamadı");

            var statusCancelled = await _context.LeaveStatuses.FirstOrDefaultAsync(s => s.Code == "Cancelled");

            request.StatusId = statusCancelled.Id;

            await _context.SaveChangesAsync();
        }
        public async Task<List<LeaveRequest>> GetAllAsync()
        {
            return await _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Include(l => l.User)
                .Include(l => l.Status)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<LeaveRequest> GetByIdAsync(int id)
        {
            return await _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Include(l => l.User)
                .Include(l => l.Status)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

    }
}
