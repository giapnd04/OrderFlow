using System.Security.Cryptography;
using System.Text;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// One-time code proving control of a user's email (ADR-002). Its own table so a resend
/// adds a fresh row instead of overwriting history. Time is passed in rather than read
/// from the clock so expiry is deterministic and testable.
/// </summary>
public class EmailVerificationOtp : AuditableEntity
{
    public const int CodeLength = 6;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    // EF Core materialisation constructor.
    private EmailVerificationOtp()
    {
    }

    public int UserId { get; private set; }

    public string Code { get; private set; } = null!;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    public static EmailVerificationOtp Create(int userId, string code, DateTime nowUtc)
    {
        if (userId <= 0)
        {
            throw new DomainException("User ID must be greater than zero.");
        }

        if (code is null || code.Length != CodeLength || !code.All(char.IsAsciiDigit))
        {
            throw new DomainException($"Verification code must be {CodeLength} digits.");
        }

        return new EmailVerificationOtp
        {
            UserId = userId,
            Code = code,
            ExpiresAt = nowUtc + Lifetime,
        };
    }

    /// <summary>Usable = not yet consumed and not yet expired.</summary>
    public bool IsUsable(DateTime nowUtc) => ConsumedAt is null && nowUtc < ExpiresAt;

    /// <summary>Constant-time comparison so response timing doesn't leak how many digits matched.</summary>
    public bool Matches(string? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Code),
            Encoding.UTF8.GetBytes(candidate));
    }

    public void Consume(DateTime nowUtc)
    {
        if (!IsUsable(nowUtc))
        {
            throw new DomainException("Verification code is expired or already used.");
        }

        ConsumedAt = nowUtc;
    }
}
