using System.Text.Json;

namespace CompanyPKDApi.Services
{
    public class JobScraperAgentGemini
    {
        private readonly GoogleAI _gemini;
        private readonly GenerativeModel _model;
        private readonly ILogger<JobScraperAgentGemini> _logger;

        public JobScraperAgentGemini(IConfiguration config, ILogger<JobScraperAgentGemini> logger)
        {
            var apiKey = config["Gemini:ApiKey"];
            _gemini = new GoogleAI(apiKey);
            _model = _gemini.GenerativeModel(model: "gemini-1.5-flash-latest");
            _logger = logger;
        }

        public async Task<JobScrapingResult> ScrapeJobsForCompany(string companyName)
        {
            var result = new JobScrapingResult { CompanyName = companyName };

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = true
                });

                var page = await browser.NewPageAsync();

                _logger.LogInformation($"[1/3] Szukam strony: {companyName}");
                var website = await FindCompanyWebsite(page, companyName);

                if (string.IsNullOrEmpty(website))
                {
                    result.Success = false;
                    result.ErrorMessage = "Nie znaleziono strony firmy";
                    return result;
                }

                result.Website = website;
                _logger.LogInformation($"Znaleziono: {website}");

                _logger.LogInformation($"[2/3] Szukam sekcji kariery...");
                var careerUrl = await FindCareerPage(page, website);

                if (string.IsNullOrEmpty(careerUrl))
                {
                    result.Success = false;
                    result.ErrorMessage = "Nie znaleziono sekcji kariery";
                    return result;
                }

                _logger.LogInformation($"Znaleziono: {careerUrl}");

                _logger.LogInformation($"[3/3] Ekstrakcja ofert...");
                var jobs = await ExtractJobOffers(page, careerUrl, companyName, website);

                result.JobOffers = jobs;
                result.Success = true;
                _logger.LogInformation($"Znaleziono {jobs.Count} ofert");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Błąd: {companyName}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<string?> FindCompanyWebsite(IPage page, string companyName)
        {
            try
            {
                var searchQuery = $"{companyName} oficjalna strona";
                var searchUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(searchQuery)}";

                _logger.LogInformation($"Szukam w DuckDuckGo: {searchUrl}");

                await page.GotoAsync(searchUrl, new()
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30000
                });

                await Task.Delay(2000);

                var urls = new List<string>();
                var links = await page.Locator("article a[href^='http'], .result__a[href^='http']").AllAsync();

                foreach (var link in links.Take(10))
                {
                    var href = await link.GetAttributeAsync("href");

                    if (string.IsNullOrEmpty(href)) continue;

                    var blacklist = new[]
                    {
                "duckduckgo.com", "youtube.com", "facebook.com",
                "linkedin.com", "wikipedia.org", "instagram.com",
                "twitter.com"
            };

                    if (!blacklist.Any(b => href.Contains(b)) && !urls.Contains(href))
                    {
                        urls.Add(href);
                        _logger.LogInformation($"  📄 {href}");
                    }
                }

                if (!urls.Any())
                {
                    _logger.LogWarning($"Brak wyników dla: {companyName}");
                    return null;
                }

                _logger.LogInformation($"Znaleziono {urls.Count} URL-i");

                if (urls.Count == 1)
                {
                    return urls.First();
                }

                // AI wybiera najlepszy
                var prompt = $@"Firma: {companyName}

URLs:
{string.Join("\n", urls.Take(5).Select((u, i) => $"{i + 1}. {u}"))}

Która jest oficjalną stroną tej firmy? Odpowiedz TYLKO numerem 1-{Math.Min(5, urls.Count)}:";

                var response = await _model.GenerateContent(prompt);
                var answer = response.Text?.Trim() ?? "1";
                var digit = new string(answer.Where(char.IsDigit).ToArray());

                if (int.TryParse(digit, out int index) && index > 0 && index <= urls.Count)
                {
                    return urls[index - 1];
                }

                return urls.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wyszukiwania");
                return null;
            }
        }
        private async Task<string?> FindCareerPage(IPage page, string website)
        {
            try
            {
                await page.GotoAsync(website, new() { WaitUntil = WaitUntilState.NetworkIdle });

                var html = await page.ContentAsync();
                var allLinks = await page.Locator("a[href]").AllAsync();

                var candidateLinks = new List<string>();
                foreach (var link in allLinks)
                {
                    var href = await link.GetAttributeAsync("href");
                    var text = (await link.TextContentAsync())?.ToLower() ?? "";

                    if (string.IsNullOrEmpty(href)) continue;

                    var keywords = new[] { "kariera", "praca", "job", "career", "rekrutacja", "join", "team", "pracuj", "zespół" };
                    if (keywords.Any(k => href.ToLower().Contains(k) || text.Contains(k)))
                    {
                        if (!href.StartsWith("http"))
                        {
                            href = new Uri(new Uri(website), href).ToString();
                        }
                        candidateLinks.Add(href);
                    }
                }

                if (!candidateLinks.Any())
                {
                    _logger.LogWarning("Nie znaleziono linków do kariery w HTML");
                    return null;
                }

                if (candidateLinks.Count == 1)
                {
                    return candidateLinks.First();
                }

                var prompt = $@"Analizujesz stronę firmy. Znalazłem następujące linki które mogą prowadzić do sekcji z ofertami pracy:

{string.Join("\n", candidateLinks.Select((l, i) => $"{i + 1}. {l}"))}

Który link najprawdopodobniej prowadzi do strony z aktualnymi ofertami pracy?

ODPOWIEDZ TYLKO NUMEREM (1-{candidateLinks.Count}).
Twoja odpowiedź (tylko cyfra):";

                var response = await _model.GenerateContent(prompt);
                var answer = response.Text?.Trim() ?? "1";

                var digit = new string(answer.Where(char.IsDigit).ToArray());

                if (int.TryParse(digit, out int index) && index > 0 && index <= candidateLinks.Count)
                {
                    return candidateLinks[index - 1];
                }

                return candidateLinks.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd znajdowania kariery");
                return null;
            }
        }

        private async Task<List<JobOffer>> ExtractJobOffers(
            IPage page,
            string careerUrl,
            string companyName,
            string website)
        {
            try
            {
                await page.GotoAsync(careerUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Task.Delay(2000);

                var html = await page.ContentAsync();

                var htmlSnippet = html.Length > 15000
                    ? html.Substring(0, 15000)
                    : html;

                var prompt = $@"Analizujesz stronę z ofertami pracy. Wyekstraktuj WSZYSTKIE oferty.

HTML:
{htmlSnippet}

Zwróć dane w formacie JSON (bez żadnego innego tekstu, tylko czysty JSON):

[
  {{
    ""title"": ""Tytuł stanowiska"",
    ""location"": ""Miasto lub Remote"",
    ""url"": ""Link do oferty (może być relatywny jak /jobs/123)""
  }}
]

Jeśli nie ma ofert, zwróć: []

WAŻNE: Odpowiedz TYLKO JSON, bez markdown, bez wyjaśnień.";

                var response = await _model.GenerateContent(prompt);
                var jsonText = response.Text?.Trim() ?? "[]";

                jsonText = jsonText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                List<JobOfferDto>? jobsData;
                try
                {
                    jobsData = JsonSerializer.Deserialize<List<JobOfferDto>>(jsonText);
                }
                catch (JsonException)
                {
                    _logger.LogWarning($"Nie udało się sparsować JSON: {jsonText}");
                    return new List<JobOffer>();
                }

                if (jobsData == null || !jobsData.Any())
                {
                    return new List<JobOffer>();
                }

                var jobs = jobsData.Select(j => new JobOffer
                {
                    CompanyName = companyName,
                    CompanyWebsite = website,
                    JobTitle = j.Title ?? "Nieznane stanowisko",
                    Location = j.Location,
                    JobUrl = ConvertToAbsoluteUrl(j.Url, careerUrl),
                    Description = null
                }).ToList();

                return jobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd ekstraktowania ofert");
                return new List<JobOffer>();
            }
        }

        private string? ConvertToAbsoluteUrl(string? url, string baseUrl)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (url.StartsWith("http")) return url;

            try
            {
                return new Uri(new Uri(baseUrl), url).ToString();
            }
            catch
            {
                return url;
            }
        }

        private class JobOfferDto
        {
            public string? Title { get; set; }
            public string? Location { get; set; }
            public string? Url { get; set; }
        }
    }
}