namespace OrderFlow.Application.Abstractions.Security;

public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash (algorithm parameters + salt + digest).</summary>
    string Hash(string password);

    /// <summary>Constant-time verification of <paramref name="password"/> against <paramref name="passwordHash"/>.</summary>
    bool Verify(string password, string passwordHash);
}
