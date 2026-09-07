using CarRepair.Auth.Application.Contracts;

namespace CarRepair.Auth.Application.Interfaces;

public interface IAuthenticateCustomerService
{
    Task<AuthenticateCustomerResponse> ExecuteAsync(AuthenticateCustomerCommand command, CancellationToken cancellationToken = default);
}
