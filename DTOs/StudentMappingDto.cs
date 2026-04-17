namespace grad.DTOs
{
    public class StudentMappingDto
    {
        public Guid StudentId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string AcademicLevel { get; set; }
        public int AcademicYear { get; set; }
        public string ParentPhoneNumber { get; set; }
        public List<string> AssignedTeachers { get; set; }
    }
}
