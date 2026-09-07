using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using CarRepair.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CarRepair.Auth.Infrastructure.SecretsManager;

public sealed class SecretsManagerConnectionStringProvider : IConnectionStringProvider
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<SecretsManagerConnectionStringProvider> _logger;
    private readonly SecretsManagerOptions _options;
    private string? _cachedConnectionString;

    public SecretsManagerConnectionStringProvider(
        IAmazonSecretsManager secretsManager,
        IOptions<SecretsManagerOptions> options,
        ILogger<SecretsManagerConnectionStringProvider> logger)
    {
        _secretsManager = secretsManager;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedConnectionString))
        {
            return _cachedConnectionString;
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionStringSecretId))
        {
            throw new InvalidOperationException("SecretsManager:ConnectionStringSecretId is not configured.");
        }

        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedConnectionString))
            {
                return _cachedConnectionString;
            }

            _logger.LogInformation("Retrieving PostgreSQL connection string from Secrets Manager secret {SecretId}", _options.ConnectionStringSecretId);

            var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = _options.ConnectionStringSecretId
            }, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.SecretString))
            {
                throw new InvalidOperationException("Secrets Manager returned an empty secret string.");
            }

            _cachedConnectionString = BuildConnectionString(response.SecretString);
            return _cachedConnectionString;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private static string BuildConnectionString(string secretString)
    {
        using var document = JsonDocument.Parse(secretString);
        var root = document.RootElement;

        foreach (var propertyName in new[] { "connectionString", "ConnectionString", "value", "Value" })
        {
            if (root.TryGetProperty(propertyName, out var connectionStringElement) && connectionStringElement.ValueKind == JsonValueKind.String)
            {
                var connectionString = connectionStringElement.GetString();
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    return connectionString;
                }
            }
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = ReadString(root, "host", "Host"),
            Port = ReadInt(root, 5432, "port", "Port"),
            Database = ReadString(root, "database", "Database"),
            Username = ReadString(root, "username", "Username"),
            Password = ReadString(root, "password", "Password"),
            Pooling = true,
            IncludeErrorDetail = false
        };

        return builder.ConnectionString;
    }

    private static int ReadInt(JsonElement element, int defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value))
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        throw new InvalidOperationException($"Required secret property '{string.Join("/", names)}' was not found.");
    }
}
