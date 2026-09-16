using System.Text;
using System.Xml.Linq;

namespace CompanyPKDApi.Services
{
    public class GUSService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<GUSService> _logger;
        private string? _sid;

        public GUSService(IConfiguration config, ILogger<GUSService> logger)
        {
            _config = config;
            _logger = logger;
            _http = new HttpClient();
            _http.BaseAddress = new Uri("https://wyszukiwarkaregon.stat.gov.pl/wsBIR/UslugaBIRzewnPubl.svc");
        }

        public async Task<bool> Login()
        {
            try
            {
                var apiKey = _config["GUS:ApiKey"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Brak GUS:ApiKey!");
                    return false;
                }


                var soapRequest = $@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
    <s:Body>
        <Zaloguj xmlns=""http://CIS/BIR/PUBL/2014/07"">
            <pKluczUzytkownika>{apiKey}</pKluczUzytkownika>
        </Zaloguj>
    </s:Body>
</s:Envelope>";

                var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/Zaloguj");

                var response = await _http.PostAsync("", content);
                var responseText = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Response: {responseText.Substring(0, Math.Min(300, responseText.Length))}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP {response.StatusCode}");
                    return false;
                }

                var doc = XDocument.Parse(responseText);
                XNamespace ns = "http://CIS/BIR/PUBL/2014/07";
                _sid = doc.Descendants(ns + "ZalogujResult").FirstOrDefault()?.Value;

                if (string.IsNullOrEmpty(_sid))
                {
                    _logger.LogError("Brak SID w odpowiedzi");
                    return false;
                }

                _logger.LogInformation($"SID: {_sid.Substring(0, 8)}...");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd logowania");
                return false;
            }
        }

        public async Task<CompanyDataGUS?> GetCompanyData(string nip)
        {
            try
            {
                if (string.IsNullOrEmpty(_sid))
                {
                    await Login();
                }

                if (string.IsNullOrEmpty(_sid)) return null;

                _logger.LogInformation($"GUS NIP: {nip}");

                var soapRequest = $@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
    <s:Body>
        <DaneSzukajPodmioty xmlns=""http://CIS/BIR/PUBL/2014/07"">
            <pParametryWyszukiwania>
                <Nip xmlns=""http://CIS/BIR/PUBL/2014/07/DataContract"">{nip}</Nip>
            </pParametryWyszukiwania>
        </DaneSzukajPodmioty>
    </s:Body>
</s:Envelope>";

                var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/DaneSzukajPodmioty");

                var request = new HttpRequestMessage(HttpMethod.Post, "");
                request.Content = content;
                request.Headers.Add("sid", _sid);

                var response = await _http.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($" HTTP {response.StatusCode}");
                    _sid = null;
                    return null;
                }

                var doc = XDocument.Parse(responseText);
                XNamespace ns = "http://CIS/BIR/PUBL/2014/07";
                var resultXml = doc.Descendants(ns + "DaneSzukajPodmiotyResult").FirstOrDefault()?.Value;

                if (string.IsNullOrEmpty(resultXml))
                {
                    _logger.LogWarning($" Brak danych");
                    return null;
                }

                var dataDoc = XDocument.Parse(resultXml);
                var dane = dataDoc.Root?.Element("dane");

                if (dane == null) return null;

                var website = dane.Element("AdresStronyInternetowej")?.Value
                           ?? dane.Element("fiz_adresEmail")?.Value;

                var result = new CompanyDataGUS
                {
                    Nazwa = dane.Element("Nazwa")?.Value,
                    AdresStronyInternetowej = website,
                    Regon = dane.Element("Regon")?.Value,
                    NIP = nip
                };

                if (!string.IsNullOrEmpty(result.AdresStronyInternetowej))
                {
                    _logger.LogInformation($"Znaleziono: {result.AdresStronyInternetowej}");
                }
                else
                {
                    _logger.LogWarning($"Brak strony WWW");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Błąd: {nip}");
                _sid = null;
                return null;
            }
        }

        public class CompanyDataGUS
        {
            public string? Nazwa { get; set; }
            public string? AdresStronyInternetowej { get; set; }
            public string? Regon { get; set; }
            public string? NIP { get; set; }
        }
    }
}