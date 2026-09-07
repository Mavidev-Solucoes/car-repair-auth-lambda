using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarRepair.Auth.Application.Contracts;
using CarRepair.Auth.Application.Interfaces;
using CarRepair.Auth.Domain.Entities;
using CarRepair.Auth.Infrastructure.Configuration;
using CarRepair.Auth.Infrastructure.SecretsManager;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarRepair.Auth.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtTokenOptions _options;
    private readonly IJwtSecretProvider _jwtSecretProvider;

    public JwtTokenGenerator(
        IOptions<JwtTokenOptions> options,
        IJwtSecretProvider jwtSecretProvider)
    {
        _options = options.Value;
        _jwtSecretProvider = jwtSecretProvider;
    }

    public JwtTokenResult Generate(Customer customer)
    {
        var secretKey = ResolveSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JWT secret key is not configured.");
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ExpirationInMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("cpf", customer.Cpf),
            new Claim("name", customer.Name),
            new Claim("role", "Customer")
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        return new JwtTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    private string ResolveSecretKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return _options.SecretKey;
        }

        return _jwtSecretProvider.GetSecretKeyAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
