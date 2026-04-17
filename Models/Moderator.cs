using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class Moderator
    {
        [Key]
        public Guid moderator_id { get; set; }

        [ForeignKey("User")]
        public Guid user_id { get; set; }

        public ApplicationUser User { get; set; }

        public int students_managed { get; set; } = 0;
        [ForeignKey("Admin")]
        public Guid admin_id { get; set; }
  
        public ApplicationUser Admin { get; set; }
        public DateTime? last_active { get; set; }

        public ICollection<ModeratorTeacher> AssignedTeachers { get; set; } = new List<ModeratorTeacher>();
    }
}
