namespace grad.DTOs
{
    public class SubjectTeacherDto
    {
        public int ClassId { get; set; }
        public Guid TeacherId { get; set; }
        public string TeacherName { get; set; }
        public string Schedule { get; set; }
        public string ClassType { get; set; }
        public decimal MonthlyPrice { get; set; }
        public bool IsFavorite { get; set; }
    }
}
