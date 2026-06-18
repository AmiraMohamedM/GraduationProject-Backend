using static grad.Controllers.TeacherController;

namespace grad.DTOs
{
    public class AddEntryTestDto
    {
        public string Title { get; set; }
        public int PassingScore { get; set; }
        public int RetakeIntervalHours { get; set; }
        public List<AddQuestionDto> Questions { get; set; }
    }
}
