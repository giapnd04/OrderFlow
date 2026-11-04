namespace OrderFlow.Application.Abstractions.Security;

/// <summary>
/// Remembers failed logins per normalized email (OF-07). Keyed by email, not by user, so an
/// address that does not exist is counted and locked exactly like one that does, and the
/// lockout cannot be used to find out which emails are registered.
/// </summary>
public interface ILoginThrottleStore
{
    /// <summary>When the current lock ends, or null if this email has never been locked.</summary>
    Task<DateTime?> GetLockedUntilAsync(string email, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(string email, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task ResetAsync(string email, CancellationToken cancellationToken = default);
}

public static class LoginThrottlePolicy
{
    public const int MaxFailures = 5;

    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
}
