using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using CarRepair.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarRepair.Auth.Infrastructure.SecretsManager;

public sealed class SecretsManagerJwtSecretProvider : IJwtSecretProvider
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<SecretsManagerJwtSecretProvider> _logger;
    private readonly SecretsManagerOptions _options;
    private string? _cachedSecretKey;

    public SecretsManagerJwtSecretProvider(
        IAmazonSecretsManager secretsManager,
        IOptions<SecretsManagerOptions> options,
        ILogger<SecretsManagerJwtSecretProvider> logger)
    {
        _secretsManager = secretsManager;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> GetSecretKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedSecretKey))
        {
            return _cachedSecretKey;
        }

        if (string.IsNullOrWhiteSpace(_options.JwtSecretId))
        {
            throw new InvalidOperationException("SecretsManager:JwtSecretId is not configured.");
        }

        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedSecretKey))
            {
                return _cachedSecretKey;
            }

            _logger.LogInformation("Retrieving JWT signing key from Secrets Manager secret {SecretId}", _options.JwtSecretId);

            var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = _options.JwtSecretId
            }, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.SecretString))
            {
                throw new InvalidOperationException("Secrets Manager returned an empty JWT secret.");
            }

            _cachedSecretKey = ParseSecretKey(response.SecretString);
            return _cachedSecretKey;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private static string ParseSecretKey(string secretString)
    {
        try
        {
            using var document = JsonDocument.Parse(secretString);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                var directValue = root.GetString();
                if (!string.IsNullOrWhiteSpace(directValue))
                {
                    return directValue;
                }
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "secretKey", "SecretKey", "value", "Value" })
                {
                    if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        var secret = value.GetString();
                        if (!string.IsNullOrWhiteSpace(secret))
                        {
                            return secret;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(secretString))
            {
                return secretString;
            }
        }

        throw new InvalidOperationException("JWT secret key could not be parsed from Secrets Manager.");
    }
}
