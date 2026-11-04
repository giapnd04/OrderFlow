namespace OrderFlow.Application.Exceptions;

/// <summary>Too many failed logins for this email; maps to 429 with a Retry-After header.</summary>
public sealed class TooManyLoginAttemptsException : Exception
{
    public TooManyLoginAttemptsException(TimeSpan retryAfter)
        : base("Too many failed login attempts. Try again later.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
