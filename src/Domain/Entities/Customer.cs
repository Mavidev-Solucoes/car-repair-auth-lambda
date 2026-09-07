namespace CarRepair.Auth.Domain.Entities;

public class Customer
{
    private Customer()
    {
    }

    public Customer(Guid id, string cpf, string name, string? email, bool isActive)
    {
        Id = id;
        Cpf = ValueObjects.Cpf.Normalize(cpf);
        Name = name;
        Email = email;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public string Cpf { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public bool IsActive { get; private set; }
}
