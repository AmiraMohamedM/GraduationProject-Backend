using System.ComponentModel.DataAnnotations;

namespace grad.DTOs
{
  
    public class CreateStudentRequestDto
    {
        [Required(ErrorMessage = "First name is required.")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number.")]
        public string? PhoneNumber { get; set; }

        [Phone(ErrorMessage = "Invalid parent phone number.")]
        public string? ParentPhoneNumber { get; set; }

        [Required(ErrorMessage = "Academic level is required.")]
        public string AcademicLevel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Class level is required.")]
        public int AcademicYear { get; set; }
        public List<Guid> TeacherIds { get; set; } = new();
    }
}
