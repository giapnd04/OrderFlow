using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Notifications;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Auth.Register;

/// <summary>
/// Self-service signup (ADR-002). No customer with this email -> create the Customer and
/// the User together, linked. A Sales-entered customer already has this email -> create
/// the User *unlinked*; the link is the "claim" and only happens after the emailed code is
/// confirmed, so merely knowing someone's email cannot take over their order history.
/// Everything (including the email) happens in one transaction: if sending fails, the
/// signup rolls back and can simply be retried.
/// </summary>
public sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _users;
    private readonly ICustomerRepository _customers;
    private readonly IEmailVerificationOtpRepository _otps;
    private readonly IPasswordHasher _hasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IEmailSender _email;
    private readonly ITransactionRunner _transactions;
    private readonly TimeProvider _clock;

    public RegisterCommandHandler(
        IUserRepository users,
        ICustomerRepository customers,
        IEmailVerificationOtpRepository otps,
        IPasswordHasher hasher,
        IOtpGenerator otpGenerator,
        IEmailSender email,
        ITransactionRunner transactions,
        TimeProvider clock)
    {
        _users = users;
        _customers = customers;
        _otps = otps;
        _hasher = hasher;
        _otpGenerator = otpGenerator;
        _email = email;
        _transactions = transactions;
        _clock = clock;
    }

    public async Task<RegisterResult> Handle(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        var email = User.Normalize(command.Email);

        if (await _users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var existingCustomer = await _customers.GetByEmailAsync(email, cancellationToken);

        if (existingCustomer is not null
            && await _users.IsCustomerLinkedAsync(existingCustomer.Id, cancellationToken))
        {
            throw new ConflictException("This customer record is already linked to another account.");
        }

        var user = User.Create(email, _hasher.Hash(command.Password), UserRole.Customer);
        var code = _otpGenerator.GenerateCode();
        var now = _clock.GetUtcNow().UtcDateTime;

        await _transactions.ExecuteAsync(
            async token =>
            {
                if (existingCustomer is null)
                {
                    var customer = Customer.Create(command.Name, email, command.Phone);

                    await _customers.AddAsync(customer, token);

                    user.LinkCustomer(customer.Id);
                }

                await _users.AddAsync(user, token);

                await _otps.AddAsync(EmailVerificationOtp.Create(user.Id, code, now), token);

                await _email.SendAsync(
                    user.Email,
                    "Verify your OrderFlow email",
                    $"Your OrderFlow verification code is {code}. It expires in " +
                    $"{(int)EmailVerificationOtp.Lifetime.TotalMinutes} minutes.",
                    token);
            },
            cancellationToken);

        return new RegisterResult(user.Id, user.Email, user.IsEmailVerified, user.CustomerId is not null);
    }
}
