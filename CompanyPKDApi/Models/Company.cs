namespace CompanyPKDApi.Models
{
    public class Company
    {
        public string? KRS { get; set; }
        public string? Nazwa { get; set; }
        public string? NIP { get; set; }
        public string? PKD_Glowny { get; set; }
        public string? PKD_Dodatkowe { get; set; }

        public bool HasPKD(string pkdPrefix)
        {
            if (string.IsNullOrEmpty(pkdPrefix)) return false;

            return (PKD_Glowny?.StartsWith(pkdPrefix) == true) ||
                   (PKD_Dodatkowe?.Contains(pkdPrefix) == true);
        }
    }
}