//using grad.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;

//namespace grad.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class HomeController : ControllerBase
//    {
//        private readonly HomeService _homeService;

//        public HomeController(HomeService homeService)
//        {
//            _homeService = homeService;
//        }

//        [HttpGet]
//        [Authorize]
//        public async Task<IActionResult> GetHomePageData()
//        {
//            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

//            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
//            {
//                return Unauthorized("User ID not found in token");
//            }

//            var result = await _homeService.GetHomeDataAsync(userId);

//            if (result == null)
//            {
//                return Ok(new { Message = "Welcome Amira! No statistics yet." });
//            }

//            return Ok(result);
//        }
//    }
//}