using static grad.Controllers.TeacherController;

namespace grad.DTOs
{
    public class AddQuestionDto
    {
        public string Text { get; set; }
        public List<AddOptionDto> Options { get; set; }
    }
}
