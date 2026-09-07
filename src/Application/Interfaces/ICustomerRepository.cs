using CarRepair.Auth.Domain.Entities;

namespace CarRepair.Auth.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByCpfAsync(string normalizedCpf, CancellationToken cancellationToken = default);
}
