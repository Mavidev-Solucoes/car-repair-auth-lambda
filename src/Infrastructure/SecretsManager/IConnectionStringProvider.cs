namespace CarRepair.Auth.Infrastructure.SecretsManager;

public interface IConnectionStringProvider
{
    Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default);
}
