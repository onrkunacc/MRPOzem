using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CRMOzem.DataAccess.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<ExpenseReport> ExpenseReports { get; set; }
        public DbSet<ExpenseDetail> ExpenseDetails { get; set; }
        public DbSet<Cariler> Carilers { get; set; }
        public DbSet<CariGruplari> CariGruplaris { get; set; }
        public DbSet<Invoices> Invoices { get; set; }
        public DbSet<DocumentCategory> DocumentCategories { get; set; }
        public DbSet<FollowDocument> FollowDocuments { get; set; }
        public DbSet<FileTransferQueue> FileTransferQueues { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveStatus> LeaveStatuses { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<UserLeaveBalance> UserLeaveBalances { get; set; }
        public DbSet<UserSupervisor> UserSupervisors { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) 
        { 
        
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ExpenseReport>()
                .HasMany(r => r.ExpenseDetails)
                .WithOne(d => d.Report)
                .HasForeignKey(d => d.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ExpenseReport>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ExpenseDetail>()
                .Property(e => e.PaymentMethod)
                .HasConversion<int>();
            builder.Entity<UserSupervisor>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserSupervisor>()
                .HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
