using grad.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Teacher
{
    [Key]
    public Guid teacher_id { get; set; }

    [ForeignKey("User")]
    public Guid user_id { get; set; }

    [ForeignKey("Admin")]
    public Guid admin_id { get; set; }

  
    public string subject { get; set; }

    public bool is_approved { get; set; } = true;

    public ApplicationUser Admin { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid? ModeratorId { get; set; }

    [ForeignKey("ModeratorId")]
    public ApplicationUser? Moderator { get; set; }
    public ICollection<StudentTeacher> AssignedStudents { get; set; } = new List<StudentTeacher>();
}