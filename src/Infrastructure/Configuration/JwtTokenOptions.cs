namespace CarRepair.Auth.Infrastructure.Configuration;

public sealed class JwtTokenOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "car-repair-auth";

    public string Audience { get; set; } = "car-repair-shop";

    public int ExpirationInMinutes { get; set; } = 60;
}
