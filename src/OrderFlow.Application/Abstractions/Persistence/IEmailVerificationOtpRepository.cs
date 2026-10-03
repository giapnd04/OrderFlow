using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IEmailVerificationOtpRepository
{
    Task AddAsync(EmailVerificationOtp otp, CancellationToken cancellationToken = default);

    /// <summary>Newest unconsumed, unexpired code for the user (tracked, for update), or null.</summary>
    Task<EmailVerificationOtp?> GetLatestUsableForUpdateAsync(
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(EmailVerificationOtp otp, CancellationToken cancellationToken = default);
}
