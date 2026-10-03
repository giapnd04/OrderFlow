using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Auth.ConfirmEmailOtp;

/// <summary>
/// Confirms the emailed code (ADR-002). Success verifies the email and, if a Sales-entered
/// customer shares it, completes the claim by linking that customer. Every failure - unknown
/// user, no live code, wrong code - returns the same generic error so the endpoint cannot be
/// used to probe which user ids exist or what state they are in.
/// </summary>
public sealed class ConfirmEmailOtpCommandHandler
    : ICommandHandler<ConfirmEmailOtpCommand, ConfirmEmailOtpResult>
{
    private const string InvalidCodeMessage = "Invalid or expired verification code.";

    private readonly IUserRepository _users;
    private readonly ICustomerRepository _customers;
    private readonly IEmailVerificationOtpRepository _otps;
    private readonly ITransactionRunner _transactions;
    private readonly TimeProvider _clock;

    public ConfirmEmailOtpCommandHandler(
        IUserRepository users,
        ICustomerRepository customers,
        IEmailVerificationOtpRepository otps,
        ITransactionRunner transactions,
        TimeProvider clock)
    {
        _users = users;
        _customers = customers;
        _otps = otps;
        _transactions = transactions;
        _clock = clock;
    }

    public async Task<ConfirmEmailOtpResult> Handle(
        ConfirmEmailOtpCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var user = await _users.GetByIdForUpdateAsync(command.UserId, cancellationToken)
            ?? throw new ValidationException(InvalidCodeMessage);

        var otp = await _otps.GetLatestUsableForUpdateAsync(user.Id, now, cancellationToken);

        if (otp is null || !otp.Matches(command.Code))
        {
            throw new ValidationException(InvalidCodeMessage);
        }

        Customer? customerToClaim = null;

        if (user.Role == UserRole.Customer && user.CustomerId is null)
        {
            customerToClaim = await _customers.GetByEmailAsync(user.Email, cancellationToken);

            // Someone else may have claimed it since registration; then verify without linking.
            if (customerToClaim is not null
                && await _users.IsCustomerLinkedAsync(customerToClaim.Id, cancellationToken))
            {
                customerToClaim = null;
            }
        }

        await _transactions.ExecuteAsync(
            async token =>
            {
                otp.Consume(now);
                user.MarkEmailVerified();

                if (customerToClaim is not null)
                {
                    user.LinkCustomer(customerToClaim.Id);
                }

                await _otps.UpdateAsync(otp, token);
                await _users.UpdateAsync(user, token);
            },
            cancellationToken);

        return new ConfirmEmailOtpResult(user.Id, user.IsEmailVerified, user.CustomerId is not null);
    }
}
