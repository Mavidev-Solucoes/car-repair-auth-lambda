namespace CarRepair.Auth.Application.Contracts;

public sealed record JwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);
