using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Auth.Login;

/// <summary>
/// Email + password -> JWT. Unknown email and wrong password are indistinguishable to the
/// caller (same exception) and cost about the same time. Login does not require a verified
/// email (ADR-002): the customer link is what verification gates, not the ability to log in.
/// </summary>
public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _tokens;

    public LoginCommandHandler(IUserRepository users, IPasswordHasher hasher, IJwtTokenGenerator tokens)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(User.Normalize(command.Email), cancellationToken);

        if (user is null)
        {
            // Burn about as much time as a real verification so response time does not reveal
            // whether the email is registered.
            _hasher.Hash(command.Password);

            throw new AuthenticationFailedException();
        }

        if (!_hasher.Verify(command.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException();
        }

        var token = _tokens.Generate(user);

        return new LoginResult(
            token.Token,
            "Bearer",
            token.ExpiresAtUtc,
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.CustomerId,
            user.IsEmailVerified);
    }
}
