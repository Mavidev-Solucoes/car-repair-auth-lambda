using CarRepair.Auth.Application.Interfaces;
using CarRepair.Auth.Domain.Entities;
using CarRepair.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRepair.Auth.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AuthDbContext _dbContext;

    public CustomerRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Customer?> GetByCpfAsync(string normalizedCpf, CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.Cpf == normalizedCpf, cancellationToken);
    }
}
