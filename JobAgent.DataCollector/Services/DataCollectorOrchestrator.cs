using System.Net.Http.Json;
using System.Text.Json;

namespace JobAgent.DataCollector.Services;

public class DataCollectorOrchestrator
{
    private readonly HttpClient _httpClient;

    public DataCollectorOrchestrator(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task RunTestCollection()
    {

        DateTime endDate = new DateTime.Today();
        DateTime startDate = endDate.AddDays(-5);
        string csvPath = "firmy_pkd_data.csv";

        var existingKrs = await LoadExistingKrs(csvPath);
        var allCompanies = new List<CompanyPkdData>();

        Console.WriteLine($"Wczytano {existingKrs.Count} już istniejących wpisów.");
        Console.WriteLine($"Zakres zbierania: {startDate:yyyy-MM-dd} do {endDate:yyyy-MM-dd}\n");

        for (DateTime currentDate = endDate; currentDate >= startDate; currentDate = currentDate.AddDays(-1))
        {
            Console.WriteLine($"\n═══ Dzień: {currentDate:yyyy-MM-dd} ═══");
            var krsNumbers = await GetBulletinForDay(currentDate);

            if (krsNumbers.Length == 0) continue;

            var uniqueKrs = krsNumbers.Distinct().ToList();

            var krsToProcess = uniqueKrs
                .Where(k => !existingKrs.Contains(k.PadLeft(10, '0')))
                .ToList();

            Console.WriteLine($"✓ Do pobrania: {krsToProcess.Count} (Pominięto: {uniqueKrs.Count - krsToProcess.Count})");

            foreach (var krs in krsToProcess)
            {
                var companyData = await GetCompanyWithPkd(krs);

                if (companyData != null)
                {
                    var formattedKrs = companyData.Krs.PadLeft(10, '0');
                    allCompanies.Add(companyData);
                    existingKrs.Add(formattedKrs);

                    Console.WriteLine($"  [OK] {formattedKrs} | {companyData.Nazwa}");

                    await AppendToCsv(companyData, csvPath);
                }

                await Task.Delay(250);
            }
        }

        Console.WriteLine($"\n Zakończono, Łączna liczba firm w pliku: {existingKrs.Count}");
    }

    private async Task<HashSet<string>> LoadExistingKrs(string path)
    {
        var krsSet = new HashSet<string>();
        if (!File.Exists(path)) return krsSet;

        var lines = await File.ReadAllLinesAsync(path);
        foreach (var line in lines.Skip(1))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^""(\d+)""");
            if (match.Success)
            {
                krsSet.Add(match.Groups[1].Value.PadLeft(10, '0'));
            }
        }
        return krsSet;
    }

    private async Task AppendToCsv(CompanyPkdData company, string path)
    {
        bool exists = File.Exists(path);
        using var writer = new StreamWriter(path, append: true);

        if (!exists)
        {
            await writer.WriteLineAsync("KRS,Nazwa,NIP,PKD_Glowny,PKD_Dodatkowe");
        }

        var pkdDodatkowe = string.Join(";", company.PkdDodatkowe);
        var line = $"\"{company.Krs.PadLeft(10, '0')}\",\"{company.Nazwa?.Replace("\"", "'")}\",\"{company.Nip}\",\"{company.PkdGlowny}\",\"{pkdDodatkowe}\"";
        await writer.WriteLineAsync(line);
    }

    private async Task<string[]> GetBulletinForDay(DateTime date)
    {
        var url = $"https://api-krs.ms.gov.pl/api/Krs/Biuletyn/{date:yyyy-MM-dd}?" +
                  $"godzinaOd=00&godzinaDo=23";

        Console.WriteLine($"Wywołuję: {url}\n");

        try
        {
            var response = await _httpClient.GetAsync(url);
            Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}\n");

            if (!response.IsSuccessStatusCode)
                return Array.Empty<string>();

            var krsNumbers = await response.Content.ReadFromJsonAsync<string[]>();
            return krsNumbers ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private async Task<CompanyPkdData?> GetCompanyWithPkd(string krs)
    {
        var krsFormatted = krs.PadLeft(10, '0');

        var company = await TryGetFromRegister(krsFormatted, "P");

        if (company == null)
        {
            company = await TryGetFromRegister(krsFormatted, "S");
        }

        return company;
    }

    private async Task<CompanyPkdData?> TryGetFromRegister(string krs, string register)
    {
        var url = $"https://api-krs.ms.gov.pl/api/krs/OdpisAktualny/{krs}?" +
                  $"rejestr={register}&format=json";

        try
        {
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json.Length < 10)
                return null;

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? nazwa = null;
            string? nip = null;
            string? pkdGlowny = null;
            var pkdDodatkowe = new List<string>();

            if (root.TryGetProperty("odpis", out var odpis))
            {
                if (odpis.TryGetProperty("dane", out var dane))
                {
                    if (dane.TryGetProperty("dzial1", out var dzial1))
                    {
                        if (dzial1.TryGetProperty("danePodmiotu", out var danePodmiotu))
                        {
                            // Nazwa
                            if (danePodmiotu.TryGetProperty("nazwa", out var nazwaEl))
                                nazwa = nazwaEl.GetString();

                            // NIP
                            if (danePodmiotu.TryGetProperty("identyfikatory", out var identyfikatory))
                            {
                                if (identyfikatory.TryGetProperty("nip", out var nipEl))
                                    nip = nipEl.GetString();
                            }
                        }
                    }

                    if (dane.TryGetProperty("dzial3", out var dzial3))
                    {
                        if (dzial3.TryGetProperty("przedmiotDzialalnosci", out var przedmiotDzialalnosci))
                        {
                            if (przedmiotDzialalnosci.TryGetProperty("przedmiotPrzewazajacejDzialalnosci", out var pkdGlownyArr))
                            {
                                if (pkdGlownyArr.ValueKind == JsonValueKind.Array && pkdGlownyArr.GetArrayLength() > 0)
                                {
                                    var pierwszy = pkdGlownyArr[0];
                                    pkdGlowny = ParsePkdCode(pierwszy);
                                }
                            }

                            // PKD dodatkowe 
                            if (przedmiotDzialalnosci.TryGetProperty("przedmiotPozostalejDzialalnosci", out var pkdDodArr))
                            {
                                if (pkdDodArr.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in pkdDodArr.EnumerateArray())
                                    {
                                        var kod = ParsePkdCode(item);
                                        if (!string.IsNullOrWhiteSpace(kod))
                                        {
                                            pkdDodatkowe.Add(kod);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(nazwa))
                return null;

            return new CompanyPkdData
            {
                Krs = krs,
                Nazwa = nazwa,
                Nip = nip,
                PkdGlowny = pkdGlowny,
                PkdDodatkowe = pkdDodatkowe
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
            return null;
        }
    }

    private string? ParsePkdCode(JsonElement pkdElement)
    {
        try
        {
            string? dzial = null;
            string? klasa = null;
            string? podklasa = null;

            if (pkdElement.TryGetProperty("kodDzial", out var dzialEl))
                dzial = dzialEl.GetString();

            if (pkdElement.TryGetProperty("kodKlasa", out var klasaEl))
                klasa = klasaEl.GetString();

            if (pkdElement.TryGetProperty("kodPodklasa", out var podklasaEl))
                podklasa = podklasaEl.GetString();

            if (!string.IsNullOrWhiteSpace(dzial))
            {
                if (!string.IsNullOrWhiteSpace(klasa) && !string.IsNullOrWhiteSpace(podklasa))
                {
                    return $"{dzial}.{klasa}.{podklasa}";
                }
                else
                {
                    return dzial;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveToCsv(List<CompanyPkdData> companies, string path)
    {
        var lines = new List<string>
        {
            "KRS,Nazwa,NIP,PKD_Glowny,PKD_Dodatkowe"
        };

        foreach (var company in companies)
        {
            var pkdDodatkowe = string.Join(";", company.PkdDodatkowe);
            var line = $"\"{company.Krs}\",\"{company.Nazwa}\",\"{company.Nip ?? ""}\",\"{company.PkdGlowny ?? ""}\",\"{pkdDodatkowe}\"";
            lines.Add(line);
        }

        await File.WriteAllLinesAsync(path, lines);
    }
}

public class CompanyPkdData
{
    public string Krs { get; set; } = "";
    public string? Nazwa { get; set; }
    public string? Nip { get; set; }
    public string? PkdGlowny { get; set; }
    public List<string> PkdDodatkowe { get; set; } = new();
}