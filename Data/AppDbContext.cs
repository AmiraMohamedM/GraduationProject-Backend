using grad.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace grad.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Student> Students { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Moderator> Moderators { get; set; }
        public DbSet<ModeratorTeacher> ModeratorTeachers { get; set; }
        public DbSet<AcademicLevel> AcademicLevels { get; set; }
        public DbSet<ClassLevel> ClassLevels { get; set; }

        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseSession> CourseSessions { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }

        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<StudentQuizResult> StudentQuizResults { get; set; }

        public DbSet<HomeworkSubmission> HomeworkSubmissions { get; set; }
        public DbSet<Message> Messages { get; set; }

        public DbSet<StudentTeacher> StudentTeachers { get; set; }
        public DbSet<StudentAbsence> StudentAbsences { get; set; }

        public DbSet<StudentRequests> StudentRequests { get; set; }
        public DbSet<ActivityLogs> ActivityLogs { get; set; }

        public DbSet<Event> Events { get; set; }
        public DbSet<UserStatistics> UserStatistics { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<LessonProgress> LessonProgress { get; set; }
        public DbSet<LessonAttempt> LessonAttempts { get; set; }
        public DbSet<StudentTask> StudentTasks { get; set; }
        public DbSet<Message> ChatMessages { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("Users");
            modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
            modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

            modelBuilder.Entity<Student>()
                .ToTable("students")
                .HasKey(s => s.student_id);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.user_id);


            modelBuilder.Entity<Teacher>()
                .ToTable("teachers")
                .HasKey(t => t.teacher_id);

            modelBuilder.Entity<Teacher>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.user_id);

            modelBuilder.Entity<Teacher>()
                .HasOne(t => t.Admin)
                .WithMany()
                .HasForeignKey(t => t.admin_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Moderator>()
                .ToTable("moderators")
                .HasKey(m => m.moderator_id);

            modelBuilder.Entity<Moderator>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.user_id);

            modelBuilder.Entity<Moderator>()
                .HasOne(m => m.Admin)
                .WithMany()
                .HasForeignKey(m => m.admin_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ModeratorTeacher>()
                .ToTable("moderator_teachers")
                .HasKey(mt => mt.id);

            modelBuilder.Entity<ModeratorTeacher>()
                .HasOne(mt => mt.Moderator)
                .WithMany(m => m.AssignedTeachers)
                .HasForeignKey(mt => mt.moderator_id);

            modelBuilder.Entity<StudentTeacher>()
                .ToTable("student_teachers")
                .HasKey(st => st.Id);

            modelBuilder.Entity<StudentTeacher>()
                .HasIndex(st => new { st.StudentId, st.TeacherId })
                .IsUnique();

            modelBuilder.Entity<StudentTeacher>()
                .HasOne(st => st.Student)
                .WithMany(s => s.AssignedTeachers)
                .HasForeignKey(st => st.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentTeacher>()
                .HasOne(st => st.Teacher)
                .WithMany(t => t.AssignedStudents)
                .HasForeignKey(st => st.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Course>()
                .HasMany(c => c.CourseSessions)
                .WithOne(cs => cs.Course)
                .HasForeignKey(cs => cs.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseSession>()
                .HasKey(cs => cs.Id);

            modelBuilder.Entity<CourseSession>()
                .Property(cs => cs.Title)
                .IsRequired();

            modelBuilder.Entity<CourseSession>()
                .HasOne(cs => cs.EntryTest)
                .WithOne(q => q.CourseSession)
                .HasForeignKey<Quiz>(q => q.CourseSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique();

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentAbsence>()
                .ToTable("student_absences")
                .HasKey(a => a.Id);

            modelBuilder.Entity<StudentAbsence>()
                .HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActivityLogs>()
                .ToTable("activity_logs")
                .HasKey(a => a.Id);

            modelBuilder.Entity<ActivityLogs>()
                .Property(a => a.Text)
                .IsRequired()
                .HasMaxLength(500);

            modelBuilder.Entity<ActivityLogs>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<ActivityLogs>()
                .HasOne(a => a.User) 
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentRequests>()
                .ToTable("student_requests")
                .HasKey(r => r.Id);

            modelBuilder.Entity<StudentRequests>()
                .HasOne(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentRequests>()
                .HasOne(r => r.CourseSession)
                .WithMany()
                .HasForeignKey(r => r.LessonId)
                .OnDelete(DeleteBehavior.Cascade);




            modelBuilder.Entity<LessonProgress>()
                .ToTable("lesson_progress");

            modelBuilder.Entity<LessonAttempt>()
                .ToTable("lesson_attempts");
            modelBuilder.Entity<StudentTask>(entity =>
            {
                entity.ToTable("student_tasks");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Title)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(t => t.RecurrenceFrequency)
                      .HasMaxLength(20);

                entity.Property(t => t.RecurringDays)
                      .HasMaxLength(50);

                entity.Property(t => t.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(t => t.Student)
                      .WithMany()
                      .HasForeignKey(t => t.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(t => t.StudentId);

                entity.HasIndex(t => new { t.StudentId, t.Date });
            });

        }
    }
}