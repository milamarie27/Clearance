using Microsoft.EntityFrameworkCore;
using OnlineClearanceSystem.Models;

namespace OnlineClearanceSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ✅ NO DbSet<User> here — users table is handled by raw SQL in HomeController
        public DbSet<AcademicPeriod>        AcademicPeriods        { get; set; }
        public DbSet<Announcement>          Announcements          { get; set; }
        public DbSet<ClearanceOrganization> ClearanceOrganizations { get; set; }
        public DbSet<ClearanceSubject>      ClearanceSubjects      { get; set; }
        public DbSet<Course>                Courses                { get; set; }
        public DbSet<Curriculum>            Curricula              { get; set; }
        public DbSet<Organization>          Organizations          { get; set; }
        public DbSet<Signatory>             Signatories            { get; set; }
        public DbSet<StatusTable>           StatusTables           { get; set; }
        public DbSet<Student>               Students               { get; set; }
        public DbSet<Subject>               Subjects               { get; set; }
        public DbSet<SubjectOffering>       SubjectOfferings       { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<ClearanceSubject>()
                .HasOne(c => c.Student)
                .WithMany()
                .HasForeignKey(c => c.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<ClearanceOrganization>()
                .HasOne(c => c.Student)
                .WithMany()
                .HasForeignKey(c => c.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Student>()
                .HasIndex(s => s.StudentId)
                .IsUnique();
        }
    }
}