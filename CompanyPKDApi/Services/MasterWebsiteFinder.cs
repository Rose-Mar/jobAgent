using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyPKDApi.Services
{
    public class MasterWebsiteFinder
    {
        private readonly KRSService _krs;
        private readonly IConfiguration _config;
        private readonly ILogger<MasterWebsiteFinder> _logger;

        public MasterWebsiteFinder(
            KRSService krs,
            IConfiguration config,
            ILogger<MasterWebsiteFinder> logger)
        {
            _krs = krs;
            _config = config;
            _logger = logger;
        }

        public async Task<WebsiteResult> FindWebsite(Company company)
        {
            var result = new WebsiteResult
            {
                KRS = company.KRS,
                Nazwa = company.Nazwa,
                NIP = company.NIP
            };

            _logger.LogInformation($"🔎 {company.Nazwa}");

            try
            {
                if (!string.IsNullOrEmpty(company.KRS))
                {
                    _logger.LogInformation($"  [1/3] KRS...");

                    var website = await _krs.GetWebsiteFromKRS(company.KRS);

                    if (!string.IsNullOrEmpty(website) && await IsWebsiteValid(website))
                    {
                        result.Website = website;
                        result.Source = "KRS";
                        result.Success = true;
                        _logger.LogInformation($"KRS: {website}");
                        return result;
                    }
                }

                _logger.LogInformation($"  [2/3] Zgadywanie...");

                var guessed = await TryGuessDomain(company.Nazwa);
                if (guessed != null)
                {
                    result.Website = guessed;
                    result.Source = "Guessed";
                    result.Success = true;
                    _logger.LogInformation($"  ✅ Zgadnięto: {guessed}");
                    return result;
                }

                var groqKey = _config["Groq:ApiKey"];
                _logger.LogInformation($"DEBUG: Groq key exists: {!string.IsNullOrEmpty(groqKey)}");
                if (!string.IsNullOrEmpty(groqKey))
                {
                    _logger.LogInformation($"  [3/3] Groq AI...");

                    var searched = await SearchWithGroq(company.Nazwa, groqKey);
                    if (searched != null)
                    {
                        result.Website = searched;
                        result.Source = "GroqAI";
                        result.Success = true;
                        _logger.LogInformation($"  ✅ Groq: {searched}");
                        return result;
                    }
                }

                result.Success = false;
                result.ErrorMessage = "Nie znaleziono";
                _logger.LogWarning($"Brak");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<string?> SearchWithGroq(string? companyName, string apiKey)
        {
            if (string.IsNullOrEmpty(companyName)) return null;

            try
            {
                _logger.LogInformation($"Groq request dla: {companyName}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var prompt = $@"Jaka jest oficjalna strona internetowa firmy: {companyName}

To jest polska firma. Podaj TYLKO URL (np. https://example.pl).
Jeśli nie znasz, zwróć: BRAK";

                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            },
                    temperature = 0.1,
                    max_tokens = 50
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

                _logger.LogInformation($"Groq HTTP: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Groq błąd: {errorText}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Groq raw response: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}");

                var groqResponse = JsonSerializer.Deserialize<GroqResponse>(responseJson);

                var answer = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "";

                _logger.LogInformation($"Groq answer: '{answer}'");

                answer = answer
                    .Replace("```", "")
                    .Replace("http://", "https://")
                    .Replace("Odpowiedź:", "")
                    .Replace("URL:", "")
                    .Trim();


                if (answer.Contains("BRAK") ||
                    answer.Contains("nie znam") ||
                    answer.Contains("Nie mam") ||
                    string.IsNullOrEmpty(answer) ||
                    answer.Length > 100)
                {
                    _logger.LogWarning($"Odrzucono odpowiedź (BRAK/za długa)");
                    return null;
                }

                if (!answer.StartsWith("https://") && !answer.StartsWith("http://"))
                {
                    answer = "https://" + answer;
                }

                _logger.LogInformation($"URL do walidacji: '{answer}'");

                if (Uri.TryCreate(answer, UriKind.Absolute, out var uri))
                {
                    var cleanUrl = $"{uri.Scheme}://{uri.Host}";
                    _logger.LogInformation($"Clean URL: '{cleanUrl}'");

                    if (await IsWebsiteValid(cleanUrl))
                    {
                        _logger.LogInformation($"Walidacja OK: {cleanUrl}");
                        return cleanUrl;
                    }
                    else
                    {
                        _logger.LogWarning($"Walidacja failed dla: {cleanUrl}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Nieprawidłowy URI: '{answer}'");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Groq exception");
                return null;
            }
        }
        private async Task<string?> TryGuessDomain(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var clean = name.ToLower()
                .Replace(" sa", "").Replace(" s.a.", "")
                .Replace(" sp. z o.o.", "").Replace(" sp z o o", "")
                .Replace(" spółka z ograniczoną odpowiedzialnością", "")
                .Replace(" spółka akcyjna", "")
                .Replace(" spółka jawna", "")
                .Replace("'", "").Replace("\"", "")
                .Replace(" ", "").Replace(".", "");

            clean = clean
                .Replace("ą", "a").Replace("ć", "c").Replace("ę", "e")
                .Replace("ł", "l").Replace("ń", "n").Replace("ó", "o")
                .Replace("ś", "s").Replace("ź", "z").Replace("ż", "z");

            var domains = new[]
            {
                $"https://www.{clean}.pl",
                $"https://{clean}.pl",
                $"https://www.{clean}.com"
            };

            foreach (var domain in domains)
            {
                if (await IsWebsiteValid(domain))
                {
                    return domain;
                }
            }

            return null;
        }

        private async Task<bool> IsWebsiteValid(string url)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var content = await response.Content.ReadAsStringAsync();

                if (content.ToLower().Contains("domain for sale") ||
                    content.ToLower().Contains("domena na sprzedaż"))
                {
                    return false;
                }

                return content.Length > 1000;
            }
            catch
            {
                return false;
            }
        }

        private class GroqResponse
        {
            [JsonPropertyName("choices")]
            public List<GroqChoice>? Choices { get; set; }
        }

        private class GroqChoice
        {
            [JsonPropertyName("message")]
            public GroqMessage? Message { get; set; }
        }

        private class GroqMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}