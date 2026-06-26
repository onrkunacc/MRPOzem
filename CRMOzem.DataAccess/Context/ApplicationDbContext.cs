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
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Assignee> Assignees { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<AssignmentItem> AssignmentItems { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Kurum> Kurumlar => Set<Kurum>();
        public DbSet<KurumIstatistikAylik> KurumIstatistikAyliklar => Set<KurumIstatistikAylik>();
        public DbSet<TestSutPoint> TestSutPoints { get; set; }
        public DbSet<HospitalTestMonthlyVolume> HospitalTestMonthlyVolumes { get; set; }
        public DbSet<Ihale> Ihaleler { get; set; }
        public DbSet<Birim> Birimler { get; set; }
        public DbSet<PersonelGider> PersonelGiderler { get; set; }
        public DbSet<EkGider> EkGiderler { get; set; }
        public DbSet<IhaleKalem> IhaleKalemler { get; set; }
        public DbSet<HospitalBirim> HospitalBirimler { get; set; }

        public DbSet<HospitalAylikIstatistik> HospitalAylikIstatistikler => Set<HospitalAylikIstatistik>();

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
            // ═══ HospitalAylikIstatistik (yeni Plan B) ═══
            builder.Entity<HospitalAylikIstatistik>(entity =>
            {
                entity.ToTable("HospitalAylikIstatistikler", "ozem");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Yil).IsRequired();
                entity.Property(x => x.Ay).IsRequired();
                entity.Property(x => x.GeldiMi).IsRequired();
                entity.Property(x => x.Not).HasMaxLength(2000);
                entity.Property(x => x.UpdatedAt).IsRequired();

                entity.HasOne(x => x.Hospital)
                      .WithMany(h => h.AylikIstatistikler)
                      .HasForeignKey(x => x.HospitalID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.HospitalID, x.Yil, x.Ay }).IsUnique();
            });

            builder.Entity<TestSutPoint>()
                .HasIndex(x => new { x.TestID, x.Year })
                .IsUnique();

            builder.Entity<HospitalTestMonthlyVolume>()
                .HasIndex(x => new { x.HospitalID, x.TestID, x.Year, x.Month })
                .IsUnique();

            builder.Entity<TestSutPoint>()
                .HasOne(x => x.Test)
                .WithMany(t => t.SutPoints)
                .HasForeignKey(x => x.TestID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<HospitalTestMonthlyVolume>()
                .HasOne(x => x.Hospital)
                .WithMany(h => h.MonthlyVolumes)
                .HasForeignKey(x => x.HospitalID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<HospitalTestMonthlyVolume>()
                .HasOne(x => x.Test)
                .WithMany(t => t.MonthlyVolumes)
                .HasForeignKey(x => x.TestID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TestTemplate>()
                .HasIndex(x => new { x.HospitalID, x.TestID })
                .IsUnique();

            // Birim seed data
            builder.Entity<Birim>().HasData(
                new Birim { Id = 1, BirimKodu = "BIO", BirimAdi = "Biyokimya" },
                new Birim { Id = 2, BirimKodu = "ELISA", BirimAdi = "Makro/Mikro ELISA" },
                new Birim { Id = 3, BirimKodu = "HEM", BirimAdi = "Hemogram" },
                new Birim { Id = 4, BirimKodu = "HOR", BirimAdi = "Hormon" },
                new Birim { Id = 5, BirimKodu = "KAN", BirimAdi = "Kan Sayım" },
                new Birim { Id = 6, BirimKodu = "SEDIM", BirimAdi = "Sedimentasyon" },
                new Birim { Id = 7, BirimKodu = "HEP", BirimAdi = "Hepatit" }
            );

            // Personel → Ihale ilişkisi (cascade delete)
            builder.Entity<PersonelGider>()
                .HasOne(p => p.Ihale)
                .WithMany(i => i.PersonelGiderler)
                .HasForeignKey(p => p.IhaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PersonelGider>()
                .HasOne(p => p.Birim)
                .WithMany(b => b.PersonelGiderler)
                .HasForeignKey(p => p.BirimId)
                .OnDelete(DeleteBehavior.Restrict);

            // EkGider → Ihale ilişkisi
            builder.Entity<EkGider>()
                .HasOne(e => e.Ihale)
                .WithMany(i => i.EkGiderler)
                .HasForeignKey(e => e.IhaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EkGider>()
                .HasOne(e => e.Birim)
                .WithMany(b => b.EkGiderler)
                .HasForeignKey(e => e.BirimId)
                .OnDelete(DeleteBehavior.Restrict);

            // IhaleKalem → Ihale ilişkisi
            builder.Entity<IhaleKalem>()
                .HasOne(k => k.Ihale)
                .WithMany(i => i.IhaleKalemler)
                .HasForeignKey(k => k.IhaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<IhaleKalem>()
                .HasOne(k => k.Birim)
                .WithMany(b => b.IhaleKalemler)
                .HasForeignKey(k => k.BirimId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Asset>().ToTable("Assets", "ozem");
            builder.Entity<Assignee>().ToTable("Assignees", "ozem");
            builder.Entity<Assignment>().ToTable("Assignments", "ozem");
            builder.Entity<AssignmentItem>().ToTable("AssignmentItems", "ozem");

            builder.Entity<Document>().ToTable("Documents", "ozem");
            builder.Entity<EkGider>().ToTable("EkGiderler", "ozem");
            builder.Entity<FileTransferQueue>().ToTable("FileTransferQueues", "ozem");

            builder.Entity<Hospital>().ToTable("Hospitals", "ozem");
            builder.Entity<HospitalTestMonthlyVolume>().ToTable("HospitalTestMonthlyVolumes", "ozem");

            builder.Entity<Ihale>().ToTable("Ihaleler", "ozem");
            builder.Entity<IhaleKalem>().ToTable("IhaleKalemler", "ozem");

            builder.Entity<Kurum>().ToTable("Kurumlar", "ozem");
            builder.Entity<KurumIstatistikAylik>().ToTable("KurumIstatistikAylik", "ozem");

            builder.Entity<LeaveRequest>().ToTable("LeaveRequests", "ozem");
            builder.Entity<LeaveStatus>().ToTable("LeaveStatuses", "ozem");
            builder.Entity<LeaveType>().ToTable("LeaveTypes", "ozem");

            builder.Entity<MonthlyShipment>().ToTable("MonthlyShipments", "ozem");
            builder.Entity<MonthlyStatistics>().ToTable("MonthlyStatistics", "ozem");

            builder.Entity<PersonelGider>().ToTable("PersonelGiderler", "ozem");

            builder.Entity<Test>().ToTable("Tests", "ozem");
            builder.Entity<TestTemplate>().ToTable("TestTemplates", "ozem");
            builder.Entity<TestSutPoint>().ToTable("TestSutPoints", "ozem");

            builder.Entity<Training>().ToTable("Trainings", "ozem");
            builder.Entity<TrainingAnswer>().ToTable("TrainingAnswers", "ozem");
            builder.Entity<TrainingParticipation>().ToTable("TrainingParticipations", "ozem");
            builder.Entity<TrainingQuestion>().ToTable("TrainingQuestions", "ozem");
            builder.Entity<TrainingResult>().ToTable("TrainingResults", "ozem");
            builder.Entity<TrainingVisibleUser>().ToTable("TrainingVisibleUsers", "ozem");

            builder.Entity<UserLeaveBalance>().ToTable("UserLeaveBalances", "ozem");
            builder.Entity<UserSupervisor>().ToTable("UserSupervisors", "ozem");

            builder.Entity<Birim>().ToTable("Birimler", "ozem");

            builder.Entity<HospitalBirim>(e =>
            {
                e.HasOne(x => x.Hospital)
                 .WithMany(h => h.HospitalBirimler)
                 .HasForeignKey(x => x.HospitalID)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Birim)
                 .WithMany(b => b.HospitalBirimler)
                 .HasForeignKey(x => x.BirimId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.HospitalID, x.BirimId, x.Aktif });
            });

            builder.Entity<HospitalBirim>().ToTable("HospitalBirimler", "ozem");


        }
    }
}
