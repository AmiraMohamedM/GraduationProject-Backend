namespace grad.DTOs
{
    public class AuthResponse
    {
        public string Token { get; set; }
        public Guid UserId { get; set; }
        public string Role { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Email { get; set; }
    }

}
