namespace grad.DTOs
{
    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? LanguagePref { get; set; }
        public string? DeviceId { get; set; }


        public string? AcademicLevel { get; set; }
        public int? AcademicYear { get; set; }
        public string? ParentEmail { get; set; }


        public string? Bio { get; set; }
        public string? Subject { get; set; }


        public string? Password { get; set; }
    }
}
