namespace CompanyPKDApi.Models
{
    public class JobOffer
    {
        public string? CompanyName { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? JobTitle { get; set; }
        public string? JobUrl { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public DateTime ScrapedAt { get; set; } = DateTime.Now;
    }

    public class JobScrapingResult
    {
        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<JobOffer> JobOffers { get; set; } = new();
    }
}