namespace CarRepair.Auth.Infrastructure.SecretsManager;

public interface IJwtSecretProvider
{
    Task<string> GetSecretKeyAsync(CancellationToken cancellationToken = default);
}
