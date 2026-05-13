namespace BananaAuthApi.Configuration;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "banana-auth-service";

    public string Audience { get; set; } = "banana-app";

    public int ExpirationMinutes { get; set; } = 60;

    public int RefreshExpirationDays { get; set; } = 7;
}
