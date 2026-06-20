using Microsoft.AspNetCore.Mvc;

namespace grad.Controllers
{
    [ApiController]
    [Route("api/ai")]
    public class AiToolsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AiToolsController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }
        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();

            client.BaseAddress = new Uri(
                _configuration["PythonService:BaseUrl"]!);

            client.Timeout = TimeSpan.FromMinutes(5); 

            return client;
        }

        // ==================================================
        // SUMMARIZE
        // ==================================================

        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize(IFormFile pdf)
        {
            if (pdf == null || pdf.Length == 0)
                return BadRequest("PDF file is required.");

            using var content = new MultipartFormDataContent();

            using var stream = pdf.OpenReadStream();

            content.Add(
                new StreamContent(stream),
                "file",
                pdf.FileName);

            var client = CreateClient();

            var response = await client.PostAsync(
                "/summarize",
                content);

            var result = await response.Content.ReadAsStringAsync();

            return Content(
                result,
                "application/json");
        }

        // ==================================================
        // QUIZ
        // ==================================================

        [HttpPost("quiz")]
        public async Task<IActionResult> GenerateQuiz(
            IFormFile pdf,
            int numQuestions = 5)
        {
            if (pdf == null || pdf.Length == 0)
                return BadRequest("PDF file is required.");

            using var content = new MultipartFormDataContent();

            using var stream = pdf.OpenReadStream();

            content.Add(
                new StreamContent(stream),
                "file",
                pdf.FileName);

            content.Add(
                new StringContent(numQuestions.ToString()),
                "num_questions");

            var client = CreateClient();

            var response = await client.PostAsync(
                "/quiz",
                content);

            var result = await response.Content.ReadAsStringAsync();

            return Content(
                result,
                "application/json");
        }

        // ==================================================
        // TRUE / FALSE
        // ==================================================

        [HttpPost("true-false")]
        public async Task<IActionResult> GenerateTrueFalse(
            IFormFile pdf,
            int numQuestions = 5)
        {
            if (pdf == null || pdf.Length == 0)
                return BadRequest("PDF file is required.");

            using var content = new MultipartFormDataContent();

            using var stream = pdf.OpenReadStream();

            content.Add(
                new StreamContent(stream),
                "file",
                pdf.FileName);

            content.Add(
                new StringContent(numQuestions.ToString()),
                "num_questions");

            var client = CreateClient();

            var response = await client.PostAsync(
                "/true-false",
                content);

            var result = await response.Content.ReadAsStringAsync();

            return Content(
                result,
                "application/json");
        }
    }
}