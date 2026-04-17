using grad.DTOs;

namespace grad.Services
{
    public interface IStatisticsService
    {

        Task<StudentStatisticsDto> GetStudentStatisticsAsync(Guid studentId);
        Task<IEnumerable<StudentStatisticsDto>> GetAllStudentsStatisticsAsync();
    }
}
