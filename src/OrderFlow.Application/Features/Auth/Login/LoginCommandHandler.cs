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
    private readonly ILoginThrottleStore _throttle;
    private readonly TimeProvider _clock;

    public LoginCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        IJwtTokenGenerator tokens,
        ILoginThrottleStore throttle,
        TimeProvider clock)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _throttle = throttle;
        _clock = clock;
    }

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var email = User.Normalize(command.Email);
        var now = _clock.GetUtcNow().UtcDateTime;

        // Checked first: while locked even the right password is refused, so guessing teaches
        // nothing, and no PBKDF2 work is spent on the attacker's requests.
        var lockedUntil = await _throttle.GetLockedUntilAsync(email, cancellationToken);

        if (lockedUntil > now)
        {
            throw new TooManyLoginAttemptsException(lockedUntil.Value - now);
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            // Burn about as much time as a real verification so response time does not reveal
            // whether the email is registered.
            _hasher.Hash(command.Password);

            await _throttle.RecordFailureAsync(email, now, cancellationToken);

            throw new AuthenticationFailedException();
        }

        if (!_hasher.Verify(command.Password, user.PasswordHash))
        {
            await _throttle.RecordFailureAsync(email, now, cancellationToken);

            throw new AuthenticationFailedException();
        }

        await _throttle.ResetAsync(email, cancellationToken);

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
