using grad.Models;
using System.Threading.Tasks;

namespace grad.Services
{
    public interface ITokenService
    {
        Task<string> CreateToken(ApplicationUser user);
    }
}
