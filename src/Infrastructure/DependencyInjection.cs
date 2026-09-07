using Amazon.SecretsManager;
using CarRepair.Auth.Application.Interfaces;
using CarRepair.Auth.Infrastructure.Configuration;
using CarRepair.Auth.Infrastructure.Persistence;
using CarRepair.Auth.Infrastructure.Repositories;
using CarRepair.Auth.Infrastructure.Security;
using CarRepair.Auth.Infrastructure.SecretsManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRepair.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<JwtTokenOptions>(configuration.GetSection(JwtTokenOptions.SectionName));
        services.Configure<SecretsManagerOptions>(configuration.GetSection(SecretsManagerOptions.SectionName));

        services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        services.AddSingleton<IConnectionStringProvider, SecretsManagerConnectionStringProvider>();
        services.AddSingleton<IJwtSecretProvider, SecretsManagerJwtSecretProvider>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddDbContext<AuthDbContext>((serviceProvider, optionsBuilder) =>
        {
            var connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();
            var connectionString = connectionStringProvider.GetConnectionStringAsync(CancellationToken.None).GetAwaiter().GetResult();

            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
            });
        });

        return services;
    }
}
