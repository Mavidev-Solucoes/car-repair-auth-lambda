using CarRepair.Auth.Application.Contracts;
using CarRepair.Auth.Domain.ValueObjects;
using FluentValidation;

namespace CarRepair.Auth.Application.Validation;

public sealed class AuthenticateCustomerCommandValidator : AbstractValidator<AuthenticateCustomerCommand>
{
    public AuthenticateCustomerCommandValidator()
    {
        RuleFor(command => command.Cpf)
            .NotEmpty()
            .WithMessage("CPF is required.")
            .Must(Cpf.IsValid)
            .WithMessage("CPF is invalid.");
    }
}
