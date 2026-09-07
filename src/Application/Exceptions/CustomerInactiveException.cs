namespace CarRepair.Auth.Application.Exceptions;

public sealed class CustomerInactiveException : Exception
{
    public CustomerInactiveException(string cpf)
        : base($"Customer with CPF '{cpf}' is inactive.")
    {
    }
}
