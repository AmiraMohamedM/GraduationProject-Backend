using grad.DTOs;

namespace grad.Services
{
    public interface IStudentService
    {

        Task<StudentCredentialsResponseDto> CreateStudentAsync(CreateStudentRequestDto dto);

        Task AssignTeachersAsync(Guid studentId, List<Guid> teacherIds);

 
        Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName);

        string GenerateSecurePassword();
    }
}
