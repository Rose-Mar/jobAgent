using System.Globalization;

namespace CompanyPKDApi.Services
{
    public class CompanyService
    {
        private readonly List<Company> _companies;

        public CompanyService(IConfiguration configuration)
        {
            var csvPath = configuration["CsvFilePath"] ?? "firmy.csv";
            _companies = LoadCompanies(csvPath);
        }

        public List<Company> GetAllCompanies()
        {
            return _companies;
        }


        private List<Company> LoadCompanies(string filePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                BadDataFound = null
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            return csv.GetRecords<Company>().ToList();
        }

        public List<Company> GetCompaniesByPKD(string pkd)
        {
            return _companies.Where(c => c.HasPKD(pkd)).ToList();
        }

        public List<Company> GetCompaniesByPKDs(List<string> pkds)
        {
            return _companies.Where(c =>
                pkds.Any(pkd => c.HasPKD(pkd))
            ).ToList();
        }

        public Dictionary<string, int> GetPKDStatistics()
        {
            return _companies
                .Where(c => !string.IsNullOrEmpty(c.PKD_Glowny))
                .GroupBy(c => c.PKD_Glowny!)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}