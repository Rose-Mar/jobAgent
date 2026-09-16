using System.Net.Http.Json;

namespace JobAgent.DataCollector.Services;

public class KrsBulletinService
{
    private readonly HttpClient _httpClient;

    public KrsBulletinService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<HashSet<string>> CollectFromBulletins(
        DateTime startDate,
        DateTime endDate)
    {
        var allKrs = new HashSet<string>();
        var totalDays = (endDate - startDate).Days;
        var processedDays = 0;

        Console.WriteLine($"Zbieranie biuletynów od {startDate:yyyy-MM-dd} do {endDate:yyyy-MM-dd}");
        Console.WriteLine($"Dni do przetworzenia: {totalDays}\n");

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            processedDays++;

            try
            {
                var url = $"https://api-krs.ms.gov.pl/api/Krs/Biuletyn/{date:yyyy-MM-dd}?" +
                          $"godzinaOd=00&godzinaDo=23";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var krsNumbers = await response.Content
                        .ReadFromJsonAsync<string[]>();

                    if (krsNumbers != null)
                    {
                        foreach (var krs in krsNumbers)
                        {
                            allKrs.Add(krs);
                        }
                    }
                }

                if (processedDays % 30 == 0 || processedDays == totalDays)
                {
                    var progress = processedDays * 100.0 / totalDays;
                    Console.WriteLine(
                        $"  Postęp: {processedDays}/{totalDays} ({progress:F1}%) | " +
                        $"Znaleziono: {allKrs.Count:N0} firm"
                    );
                }

                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR {date:yyyy-MM-dd}: {ex.Message}");
            }
        }

        return allKrs;
    }
}