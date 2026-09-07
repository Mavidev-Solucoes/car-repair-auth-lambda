using CarRepair.Auth.Application.Contracts;
using CarRepair.Auth.Application.Exceptions;
using CarRepair.Auth.Application.Interfaces;
using CarRepair.Auth.Domain.ValueObjects;
using FluentValidation;

namespace CarRepair.Auth.Application.Services;

public sealed class AuthenticateCustomerService : IAuthenticateCustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IValidator<AuthenticateCustomerCommand> _validator;

    public AuthenticateCustomerService(
        ICustomerRepository customerRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IValidator<AuthenticateCustomerCommand> validator)
    {
        _customerRepository = customerRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _validator = validator;
    }

    public async Task<AuthenticateCustomerResponse> ExecuteAsync(AuthenticateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedCpf = Cpf.Normalize(command.Cpf);
        var customer = await _customerRepository.GetByCpfAsync(normalizedCpf, cancellationToken);
        if (customer is null)
        {
            throw new CustomerNotFoundException(normalizedCpf);
        }

        if (!customer.IsActive)
        {
            throw new CustomerInactiveException(normalizedCpf);
        }

        var token = _jwtTokenGenerator.Generate(customer);
        return new AuthenticateCustomerResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            customer.Id,
            customer.Name);
    }
}
