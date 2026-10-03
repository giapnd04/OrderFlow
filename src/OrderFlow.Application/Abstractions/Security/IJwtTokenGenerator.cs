using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Abstractions.Security;

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
