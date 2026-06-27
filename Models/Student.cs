using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{

    public class Student
    {
        [Key]
        public Guid student_id { get; set; }

        [ForeignKey("User")]
        public Guid user_id { get; set; }

        public string ParentPhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }

        public string? ProfileImagePublicId { get; set; }
        public string AcademicLevel { get; set; } = string.Empty;
        public int AcademicYear { get; set; }
        public string? EncryptedPassword { get; set; }

        public ApplicationUser User { get; set; } = null!;

        public ICollection<StudentTeacher> AssignedTeachers { get; set; } = new List<StudentTeacher>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}

