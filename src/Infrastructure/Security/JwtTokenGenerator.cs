using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarRepair.Auth.Application.Contracts;
using CarRepair.Auth.Application.Interfaces;
using CarRepair.Auth.Domain.Entities;
using CarRepair.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarRepair.Auth.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtTokenOptions _options;

    public JwtTokenGenerator(IOptions<JwtTokenOptions> options)
    {
        _options = options.Value;
    }

    public JwtTokenResult Generate(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("JWT secret key is not configured.");
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ExpirationInMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("cpf", customer.Cpf),
            new Claim("name", customer.Name)
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
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
}
