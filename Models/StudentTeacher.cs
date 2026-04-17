using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{

    public class StudentTeacher
    {
        [Key]
        public int Id { get; set; }

        public Guid StudentId { get; set; }
        public Guid TeacherId { get; set; }

        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        [ForeignKey("TeacherId")]
        public Teacher Teacher { get; set; } = null!;
    }
}
