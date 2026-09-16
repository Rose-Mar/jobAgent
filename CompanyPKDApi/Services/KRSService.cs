using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyPKDApi.Services
{
    public class KRSService
    {
        private readonly HttpClient _http;
        private readonly ILogger<KRSService> _logger;

        public KRSService(ILogger<KRSService> logger)
        {
            _http = new HttpClient();
            _logger = logger;
        }

        public async Task<string?> GetWebsiteFromKRS(string krs)
        {
            try
            {
                var krsClean = krs.TrimStart('0');
                _logger.LogInformation($"  🔍 KRS API: {krsClean}");

                var url = $"https://rejestr.io/api/v1/krs/{krsClean}";

                var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"KRS API błąd: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<RejestrIOResponse>(json);

                var website = data?.Entry?.Www;

                if (!string.IsNullOrEmpty(website))
                {
                    website = NormalizeUrl(website);
                    _logger.LogInformation($"KRS: {website}");
                    return website;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Błąd KRS: {krs}");
                return null;
            }
        }

        private string NormalizeUrl(string url)
        {
            url = url.Trim().ToLower();
            if (!url.StartsWith("http"))
            {
                url = "https://" + url;
            }
            return url;
        }

        private class RejestrIOResponse
        {
            [JsonPropertyName("entry")]
            public RejestrIOEntry? Entry { get; set; }
        }

        private class RejestrIOEntry
        {
            [JsonPropertyName("www")]
            public string? Www { get; set; }
        }
    }
}