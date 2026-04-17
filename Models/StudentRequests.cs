using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class StudentRequests
    {
        public Guid Id { get; set; }

        public Guid StudentId { get; set; }
        public Student Student { get; set; }

        public int LessonId { get; set; }
        public CourseSession CourseSession { get; set; }

        public string Type { get; set; } = string.Empty; 

        public int CurrentCount { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending"; 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}