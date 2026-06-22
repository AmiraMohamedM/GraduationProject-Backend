namespace grad.DTOs
{
    public class CreateCourseDto
    {
        public string Title { get; set; }
        public string AcademicLevel { get; set; }
        public int AcademicYear { get; set; }
        public string? Introduction { get; set; }
        public IFormFile? Picture { get; set; }
    }
}