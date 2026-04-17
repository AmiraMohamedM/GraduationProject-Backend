using System.ComponentModel.DataAnnotations;

namespace grad.DTOs
{

    public class AssignTeacherDto
    {
        [Required]
        public List<Guid> TeacherIds { get; set; } = new();
    }
}
