namespace grad.DTOs
{
    public class AddLessonDto
    {
        public string Title { get; set; }

        public List<IFormFile>? Files { get; set; }

        public int AvailableDays { get; set; }

        public int MaxViews { get; set; }

        public IFormFile? HomeworkFile { get; set; }

        public bool HasEntryTest { get; set; }
    }
}
