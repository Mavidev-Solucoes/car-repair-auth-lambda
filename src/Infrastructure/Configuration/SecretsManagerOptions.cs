namespace CarRepair.Auth.Infrastructure.Configuration;

public sealed class SecretsManagerOptions
{
    public const string SectionName = "SecretsManager";

    public string ConnectionStringSecretId { get; set; } = string.Empty;

    public string JwtSecretId { get; set; } = string.Empty;
}
