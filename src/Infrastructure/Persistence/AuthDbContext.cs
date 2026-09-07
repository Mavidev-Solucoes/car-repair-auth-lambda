using CarRepair.Auth.Domain.Entities;
using CarRepair.Auth.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarRepair.Auth.Infrastructure.Persistence;

public sealed class AuthDbContext : DbContext
{
    private readonly DatabaseOptions _databaseOptions;

    public AuthDbContext(DbContextOptions<AuthDbContext> options, IOptions<DatabaseOptions> databaseOptions)
        : base(options)
    {
        _databaseOptions = databaseOptions.Value;
    }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Customer>();
        entity.ToTable(_databaseOptions.CustomersTableName, _databaseOptions.Schema);
        entity.HasKey(customer => customer.Id);
        entity.Property(customer => customer.Id).HasColumnName("id");
        entity.Property(customer => customer.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        entity.Property(customer => customer.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(customer => customer.Email).HasColumnName("email").HasMaxLength(200);
        entity.Property(customer => customer.IsActive).HasColumnName("is_active").IsRequired();
        entity.HasIndex(customer => customer.Cpf).HasDatabaseName("ix_customers_cpf");
    }
}
