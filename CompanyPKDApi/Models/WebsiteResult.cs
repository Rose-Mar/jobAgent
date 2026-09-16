namespace CompanyPKDApi.Models
{
    public class WebsiteResult
    {
        public string? KRS { get; set; }
        public string? Nazwa { get; set; }
        public string? NIP { get; set; }
        public string? Website { get; set; }
        public string? Source { get; set; } 
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.Now;
    }
}