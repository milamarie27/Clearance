using Microsoft.EntityFrameworkCore;
using OnlineClearanceSystem.Models;

namespace OnlineClearanceSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // User & signatures
        public DbSet<User>          Users          { get; set; }
        public DbSet<UserSignature> UserSignatures { get; set; }

        // Academic
        public DbSet<Course>        Courses        { get; set; }
        public DbSet<Curriculum>    Curriculum     { get; set; }
        public DbSet<Section>       Sections       { get; set; }
        public DbSet<AcademicPeriod> AcademicPeriods { get; set; }
        public DbSet<Subject>       Subjects       { get; set; }
        public DbSet<SubjectOffering> SubjectOfferings { get; set; }

        // Org & clearance
        public DbSet<Organization>          Organizations          { get; set; }
        public DbSet<ClearanceSubject>      ClearanceSubjects      { get; set; }
        public DbSet<ClearanceOrganization> ClearanceOrganizations { get; set; }

        // Other
        public DbSet<Announcement> Announcements { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            mb.Entity<User>()
                .HasOne(u => u.Curriculum)
                .WithMany()
                .HasForeignKey(u => u.CurriculumId)
                .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<User>()
                .HasOne(u => u.Signature)
                .WithOne(s => s.User)
                .HasForeignKey<UserSignature>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<UserSignature>()
                .HasIndex(s => s.UserId)
                .IsUnique();

            mb.Entity<Curriculum>()
                .HasOne(c => c.Course)
                .WithMany(co => co.Curricula)
                .HasForeignKey(c => c.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Section>()
                .HasOne(s => s.Course)
                .WithMany()
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Organization>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<Organization>()
                .HasOne(o => o.Curriculum)
                .WithMany()
                .HasForeignKey(o => o.CurriculumId)
                .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<SubjectOffering>()
                .HasOne(so => so.Subject)
                .WithMany()
                .HasForeignKey(so => so.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<SubjectOffering>()
                .HasOne(so => so.User)
                .WithMany()
                .HasForeignKey(so => so.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<SubjectOffering>()
                .HasOne(so => so.Period)
                .WithMany()
                .HasForeignKey(so => so.PeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<SubjectOffering>()
                .HasIndex(so => so.MisCode)
                .IsUnique();

            mb.Entity<ClearanceOrganization>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.StudentNumber, e.Position })
                      .IsUnique();
            });
        }
    }
}
