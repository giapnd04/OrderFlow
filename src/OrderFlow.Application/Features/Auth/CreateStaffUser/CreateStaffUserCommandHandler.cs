using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Auth.CreateStaffUser;

/// <summary>
/// An Administrator provisions an internal account (Sales / Warehouse / Administrator).
/// Staff are never self-service (ADR-002), carry no customer link, and are created
/// verified - the administrator vouches for the address, so there is no OTP step.
/// </summary>
public sealed class CreateStaffUserCommandHandler
    : ICommandHandler<CreateStaffUserCommand, CreateStaffUserResult>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public CreateStaffUserCommandHandler(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<CreateStaffUserResult> Handle(
        CreateStaffUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = User.Normalize(command.Email);

        if (await _users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.Create(email, _hasher.Hash(command.Password), command.Role, isEmailVerified: true);

        await _users.AddAsync(user, cancellationToken);

        return new CreateStaffUserResult(user.Id, user.Email, user.Role.ToString());
    }
}
