using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class ModeratorTeacher
    {
        [Key]
        public int id { get; set; }

        public Guid moderator_id { get; set; }
        public Guid teacher_user_id { get; set; }

        [ForeignKey("moderator_id")]
        public Moderator Moderator { get; set; }
    }
}
