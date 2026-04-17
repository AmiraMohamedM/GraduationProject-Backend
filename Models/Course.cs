using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace grad.Models
{
    public class Course
    {
        [Key]
        public int Id { get; set; }

        public Guid TeacherId { get; set; }
        public Teacher Teacher { get; set; }

        public string Title { get; set; } = string.Empty;

        public string AcademicLevel { get; set; } = string.Empty;

        public int AcademicYear { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<CourseSession> CourseSessions { get; set; }
            = new List<CourseSession>();

        public ICollection<Quiz> Quizzes { get; set; }
            = new List<Quiz>();
    }
}