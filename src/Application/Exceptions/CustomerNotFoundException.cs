namespace CarRepair.Auth.Application.Exceptions;

public sealed class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(string cpf)
        : base($"Customer with CPF '{cpf}' was not found.")
    {
    }
}
