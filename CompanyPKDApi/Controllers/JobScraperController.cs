
namespace CompanyPKDApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobScraperController : ControllerBase
    {
        private readonly JobScraperAgentGemini _agent;
        private readonly ILogger<JobScraperController> _logger;

        public JobScraperController(
            JobScraperAgentGemini agent,
            ILogger<JobScraperController> logger)
        {
            _agent = agent;
            _logger = logger;
        }

        /// <summary>
        /// Scrape oferty pracy dla jednej firmy
        /// </summary>
        /// <param name="companyName">Nazwa firmy (np. CD Projekt)</param>
        [HttpGet("scrape/{companyName}")]
        public async Task<ActionResult> ScrapeCompany(string companyName)
        {
            _logger.LogInformation($"🔍 Rozpoczynam scraping: {companyName}");
            var result = await _agent.ScrapeJobsForCompany(companyName);
            return Ok(result);
        }

        [HttpGet("test-browser")]
        public async Task<ActionResult> TestBrowser()
        {
            try
            {
                using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false }); // widoczna przeglądarka!
                var page = await browser.NewPageAsync();

                await page.GotoAsync("https://www.google.com");
                var title = await page.TitleAsync();

                await Task.Delay(3000); 

                return Ok(new { Success = true, Title = title });
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Error = ex.Message });
            }
        }
    }
}