namespace OrderFlow.Application.Exceptions;

/// <summary>
/// Credentials were wrong. Deliberately carries no detail about which part was wrong
/// (unknown email vs. bad password) so the API can't be used to enumerate accounts.
/// Maps to HTTP 401 at the API boundary.
/// </summary>
public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException()
        : base("Invalid email or password.")
    {
    }
}
