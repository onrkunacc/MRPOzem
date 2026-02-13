using CRMOzem.Core.Inventory;
using CRMOzem.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

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
        public DbSet<Test> Tests { get; set; }
        public DbSet<TestTemplate> TestTemplates { get; set; }
        public DbSet<MonthlyStatistics> MonthlyStatistics { get; set; }
        public DbSet<MonthlyShipment> MonthlyShipments { get; set; }
        public DbSet<Hospital> Hospitals { get; set; }
        public DbSet<Training> Trainings => Set<Training>();
        public DbSet<TrainingParticipation> TrainingParticipations => Set<TrainingParticipation>();
        public DbSet<TrainingVisibleUser> TrainingVisibleUsers { get; set; }
        public DbSet<TrainingQuestion> TrainingQuestions { get; set; }
        public DbSet<TrainingAnswer> TrainingAnswers { get; set; }
        public DbSet<TrainingResult> TrainingResults { get; set; }
        public DbSet<Tender> Tenders { get; set; }
        public DbSet<TenderTest> TenderTests { get; set; }
        public DbSet<TenderExtraCost> TenderExtraCosts { get; set; }
        public DbSet<TenderPersonnel> TenderPersonnels { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Assignee> Assignees { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<AssignmentItem> AssignmentItems { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Kurum> Kurumlar => Set<Kurum>();
        public DbSet<KurumIstatistikAylik> KurumIstatistikAyliklar => Set<KurumIstatistikAylik>();

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

            builder.Entity<MonthlyStatistics>()
                .HasIndex(ms => new { ms.HospitalID, ms.TestID, ms.Month })
                .IsUnique();

            builder.Entity<MonthlyShipment>()
                .HasIndex(ms => new { ms.HospitalID, ms.TestID, ms.Month })
                .IsUnique();

            builder.Entity<Training>()
                .HasMany(t => t.Participations)
                .WithOne(p => p.Training)
                .HasForeignKey(p => p.TrainingId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Training>()
            .Property(t => t.Title).HasMaxLength(200).IsRequired();
            builder.Entity<Training>()
            .Property(t => t.Link).HasMaxLength(500).IsRequired();


            builder.Entity<TrainingParticipation>()
            .HasIndex(p => new { p.TrainingId, p.UserId }).IsUnique();

            builder.Entity<Asset>(e =>
            {
                e.Property(x => x.Status).HasConversion<string>();
                e.HasIndex(x => x.SerialNumber);
                e.HasIndex(x => x.InventoryTag);
                e.HasIndex(x => x.ImeiNo);
            });

            builder.Entity<Assignee>(e =>
            {
                e.HasIndex(x => x.UserId);
                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AssignmentItem>(e =>
            {
                e.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.AssetId, x.ReturnedAtUtc });
            });

            builder.Entity<Document>(e => e.HasIndex(x => x.AssignmentId));

            builder.Entity<Kurum>(entity =>
            {
                entity.ToTable("Kurumlar");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Ad)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(x => x.IhaleNumarasi)
                      .HasMaxLength(100);

                entity.Property(x => x.AktifMi)
                      .HasDefaultValue(true);

                entity.HasMany(x => x.IstatistikAyliklar)
                      .WithOne(x => x.Kurum)
                      .HasForeignKey(x => x.KurumId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<KurumIstatistikAylik>(entity =>
            {
                entity.ToTable("KurumIstatistikAylik");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Yil).IsRequired();
                entity.Property(x => x.Ay).IsRequired();
                entity.Property(x => x.GeldiMi).IsRequired();

                entity.Property(x => x.Not)
                      .HasMaxLength(2000);

                entity.Property(x => x.UpdatedAt)
                      .IsRequired();

                // Aynı kurum + yıl + ay için tek kayıt
                entity.HasIndex(x => new { x.KurumId, x.Yil, x.Ay })
                      .IsUnique();
            });
        }
    }
}
