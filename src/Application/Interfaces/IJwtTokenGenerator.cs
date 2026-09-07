using CarRepair.Auth.Application.Contracts;
using CarRepair.Auth.Domain.Entities;

namespace CarRepair.Auth.Application.Interfaces;

public interface IJwtTokenGenerator
{
    JwtTokenResult Generate(Customer customer);
}
