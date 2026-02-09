using BawabaUNI.Models;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BawabaUNI.Models.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets with updated names
        public DbSet<Visits> Visits { get; set; }
        public DbSet<HeroImage> HeroImages { get; set; }
        public DbSet<Partner> Partners { get; set; }
        public DbSet<University> Universities { get; set; }
        public DbSet<DocumentRequired> DocumentsRequired { get; set; }
        public DbSet<HousingOption> HousingOptions { get; set; }
        public DbSet<Faculty> Faculties { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<StudyPlanYear> StudyPlanYears { get; set; } // Updated
        public DbSet<StudyPlanMedia> StudyPlanMedia { get; set; }
        public DbSet<StudyPlanSection> StudyPlanSections { get; set; } // Updated
        public DbSet<AcademicMaterial> AcademicMaterials { get; set; }
        public DbSet<JobOpportunity> JobOpportunities { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseFeedback> CourseFeedbacks { get; set; }
        public DbSet<LessonLearned> LessonsLearned { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<Advertisement> Advertisements { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<StudentCourse> StudentCourses { get; set; }
        public DbSet<ConsultationRequest> ConsultationRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            

            // University relationships (remain the same)
            modelBuilder.Entity<University>()
                .HasMany(u => u.DocumentsRequired)
                .WithOne(d => d.University)
                .HasForeignKey(d => d.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<University>()
                .HasMany(u => u.HousingOptions)
                .WithOne(h => h.University)
                .HasForeignKey(h => h.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<University>()
                .HasMany(u => u.Faculties)
                .WithOne(f => f.University)
                .HasForeignKey(f => f.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);

            // Faculty relationships (updated)
            modelBuilder.Entity<Faculty>()
                .HasMany(f => f.StudyPlanYears) // Updated: One-to-Many
                .WithOne(sp => sp.Faculty)
                .HasForeignKey(sp => sp.FacultyId)
                .OnDelete(DeleteBehavior.Cascade); // Delete all study plan years when faculty is deleted

            modelBuilder.Entity<Faculty>()
                .HasMany(f => f.SpecializationList)
                .WithOne(s => s.Faculty)
                .HasForeignKey(s => s.FacultyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Faculty>()
                .HasMany(f => f.JobOpportunities)
                .WithOne(j => j.Faculty)
                .HasForeignKey(j => j.FacultyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Study Plan Year relationships
            modelBuilder.Entity<StudyPlanYear>()
                .HasMany(sp => sp.StudyPlanMedia)
                .WithOne(m => m.StudyPlanYear)
                .HasForeignKey(m => m.StudyPlanYearId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudyPlanYear>()
                .HasMany(sp => sp.Sections)
                .WithOne(sec => sec.StudyPlanYear)
                .HasForeignKey(sec => sec.StudyPlanYearId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudyPlanYear>()
                .HasMany(sp => sp.AcademicMaterials)
                .WithOne(a => a.StudyPlanYear)
                .HasForeignKey(a => a.StudyPlanYearId)
                .OnDelete(DeleteBehavior.NoAction); // Set null on delete

            // Study Plan Section relationships
            modelBuilder.Entity<StudyPlanSection>()
                .HasMany(s => s.AcademicMaterials)
                .WithOne(a => a.StudyPlanSection)
                .HasForeignKey(a => a.StudyPlanSectionId)
                .OnDelete(DeleteBehavior.NoAction); // Set null on delete

            // Course relationships (remain the same)
            modelBuilder.Entity<Course>()
                .HasMany(c => c.LessonsLearned)
                .WithOne(l => l.Course)
                .HasForeignKey(l => l.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Course>()
                .HasMany(c => c.Videos)
                .WithOne(v => v.Course)
                .HasForeignKey(v => v.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Course>()
                .HasMany(c => c.StudentCourses)
                .WithOne(sc => sc.Course)
                .HasForeignKey(sc => sc.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Student relationships (remain the same)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.ApplicationUser)
                .WithOne(u => u.StudentProfile)
                .HasForeignKey<Student>(s => s.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Student>()
                .HasMany(s => s.StudentCourses)
                .WithOne(sc => sc.Student)
                .HasForeignKey(sc => sc.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // StudentCourse as join table
            modelBuilder.Entity<StudentCourse>()
                .HasKey(sc => new { sc.StudentId, sc.CourseId });

            // Unique constraints (updated for new entities)
            modelBuilder.Entity<University>()
                .HasIndex(u => u.NameEnglish)
                .IsUnique();

            modelBuilder.Entity<University>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("[Email] IS NOT NULL");

            modelBuilder.Entity<Faculty>()
                .HasIndex(f => new { f.NameEnglish, f.UniversityId })
                .IsUnique();

            // Each faculty should have unique study plan year numbers
            modelBuilder.Entity<StudyPlanYear>()
                .HasIndex(sp => new { sp.FacultyId, sp.YearNumber })
                .IsUnique();

            modelBuilder.Entity<StudyPlanYear>()
                .HasIndex(sp => new { sp.FacultyId, sp.YearName })
                .IsUnique();

            modelBuilder.Entity<Course>()
                .HasIndex(c => c.NameEnglish)
                .IsUnique();

            modelBuilder.Entity<AcademicMaterial>()
                .HasIndex(a => a.Code)
                .IsUnique();

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.ApplicationUserId)
                .IsUnique();

            // Additional indexes for study plan sections
            modelBuilder.Entity<StudyPlanSection>()
                .HasIndex(s => new { s.StudyPlanYearId, s.Name })
                .IsUnique();

            // Configure decimal precision
            modelBuilder.Entity<Course>()
                .Property(c => c.Price)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Course>()
                .Property(c => c.Discount)
                .HasPrecision(5, 2);

            // Configure enums as strings
            modelBuilder.Entity<AcademicMaterial>()
                .Property(a => a.Type)
                .HasConversion<string>();


            modelBuilder.Entity<Advertisement>()
                .Property(a => a.Status)
                .HasConversion<string>();

            modelBuilder.Entity<StudyPlanYear>()
                .Property(sp => sp.Type)
                .HasConversion<string>();

            // Add indexes for performance
            modelBuilder.Entity<University>()
                .HasIndex(u => u.IsTrending);

            modelBuilder.Entity<University>()
                .HasIndex(u => u.City);

            modelBuilder.Entity<Course>()
                .HasIndex(c => c.Classification);

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.Date);

            modelBuilder.Entity<StudyPlanYear>()
                .HasIndex(sp => sp.Type);

            modelBuilder.Entity<StudentCourse>()
                .HasIndex(sc => sc.EnrollmentStatus);

            modelBuilder.Entity<StudentCourse>()
                .HasIndex(sc => sc.EnrollmentDate);
        }

        // Override SaveChanges (remains the same)
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity && (
                    e.State == EntityState.Added ||
                    e.State == EntityState.Modified ||
                    e.State == EntityState.Deleted));

            foreach (var entry in entries)
            {
                var entity = (BaseEntity)entry.Entity;
                var now = DateTime.UtcNow;

                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = now;
                    entity.UpdatedAt = null;
                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entity.IsDeleted = true;
                    entity.DeletedAt = now;
                }
            }
        }
    }
}