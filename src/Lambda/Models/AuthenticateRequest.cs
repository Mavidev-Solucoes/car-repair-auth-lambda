namespace CarRepair.Auth.Lambda.Models;

public sealed class AuthenticateRequest
{
    public string Cpf { get; set; } = string.Empty;
}
