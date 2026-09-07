namespace CarRepair.Auth.Application.Contracts;

public sealed record AuthenticateCustomerResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid CustomerId,
    string CustomerName,
    string TokenType = "Bearer");
