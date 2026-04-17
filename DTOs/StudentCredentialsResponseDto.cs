namespace grad.DTOs
{

    public class StudentCredentialsResponseDto
    {
        public Guid StudentId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> AssignedTeacherNames { get; set; } = new();
    }
}
