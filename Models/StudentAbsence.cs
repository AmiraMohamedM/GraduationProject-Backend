using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{

    public class StudentAbsence
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public Guid StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public DateTime AbsenceDate { get; set; } = DateTime.UtcNow;

        public string? Note { get; set; }

        public Guid? RecordedBy { get; set; }
    }
}
