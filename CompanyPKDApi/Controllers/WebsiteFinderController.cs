
namespace CompanyPKDApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebsiteFinderController : ControllerBase
    {
        private readonly CompanyService _companyService;
        private readonly MasterWebsiteFinder _finder;
        private readonly ILogger<WebsiteFinderController> _logger;

        public WebsiteFinderController(
            CompanyService companyService,
            MasterWebsiteFinder finder,
            ILogger<WebsiteFinderController> logger)
        {
            _companyService = companyService;
            _finder = finder;
            _logger = logger;
        }

        /// <summary>
        /// TEST - Znajdź strony WWW dla pierwszych N firm
        /// </summary>
        [HttpGet("test-first/{count}")]
        public async Task<ActionResult> TestFirst(int count = 10)
        {
            if (count < 1 || count > 100)
            {
                return BadRequest("Count musi być między 1-100");
            }


            var allCompanies = _companyService.GetAllCompanies();

            var testCompanies = allCompanies.Take(count).ToList();

            _logger.LogInformation($"Znaleziono {testCompanies.Count} firm do przetworzenia");

            var results = new List<WebsiteResult>();

            foreach (var company in testCompanies)
            {
                var result = await _finder.FindWebsite(company);
                results.Add(result);

                await Task.Delay(500);
            }

            var successCount = results.Count(r => r.Success);
            var successRate = successCount * 100.0 / results.Count;

            var sourceStats = results
                .Where(r => r.Success)
                .GroupBy(r => r.Source)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .ToList();

            _logger.LogInformation($"koniec: {successCount}/{count} ({successRate:F1}%)");

            return Ok(new
            {
                TotalProcessed = results.Count,
                SuccessCount = successCount,
                SuccessRate = $"{successRate:F1}%",
                SourceBreakdown = sourceStats,
                Results = results
            });
        }

        /// <summary>
        /// Znajdź stronę dla konkretnej firmy po KRS
        /// </summary>
        [HttpGet("find-by-krs/{krs}")]
        public async Task<ActionResult> FindByKRS(string krs)
        {
            var allCompanies = _companyService.GetAllCompanies();
            var company = allCompanies.FirstOrDefault(c => c.KRS == krs);

            if (company == null)
            {
                return NotFound($"Nie znaleziono firmy z KRS: {krs}");
            }

            var result = await _finder.FindWebsite(company);
            return Ok(result);
        }


        /// <summary>
        /// TEST - Znajdź strony WWW dla pierwszych N firm z danego PKD
        /// </summary>
        /// <param name="pkd">Numer PKD (np. 62.01.Z)</param>
        /// <param name="count">Liczba firm do przetestowania (domyślnie 10, max 100)</param>
        [HttpGet("test-by-pkd/{pkd}/{count?}")]
        public async Task<ActionResult> TestByPKD(string pkd, int count = 10)
        {
            if (count < 1 || count > 100)
            {
                return BadRequest("Count musi być między 1-100");
            }


            var companies = _companyService.GetCompaniesByPKD(pkd).Take(count).ToList();

            if (!companies.Any())
            {
                return NotFound($"Nie znaleziono firm z PKD: {pkd}");
            }

            _logger.LogInformation($"Znaleziono {companies.Count} firm do przetworzenia");

            var results = new List<WebsiteResult>();

            foreach (var company in companies)
            {
                var result = await _finder.FindWebsite(company);
                results.Add(result);

                await Task.Delay(500);
            }

            var successCount = results.Count(r => r.Success);
            var successRate = successCount * 100.0 / results.Count;

            var sourceStats = results
                .Where(r => r.Success)
                .GroupBy(r => r.Source)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .ToList();

            _logger.LogInformation($"koniec: {successCount}/{count} ({successRate:F1}%)");

            return Ok(new
            {
                PKD = pkd,
                TotalProcessed = results.Count,
                SuccessCount = successCount,
                SuccessRate = $"{successRate:F1}%",
                SourceBreakdown = sourceStats,
                Results = results
            });
        }
    }
}