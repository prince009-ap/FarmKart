namespace FarmKart.Application.Options;

public class JwtOptions
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; }

    // Cookie configuration properties
    public string CookieName { get; set; } = "FarmKartAuth";
    public bool CookieSecure { get; set; } = true;
    public string CookieSameSite { get; set; } = "Lax";
}
