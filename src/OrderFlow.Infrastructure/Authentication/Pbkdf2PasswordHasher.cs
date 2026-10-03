using System.Security.Cryptography;
using OrderFlow.Application.Abstractions.Security;

namespace OrderFlow.Infrastructure.Authentication;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a per-password random salt. The stored string is self-describing
/// (<c>PBKDF2-SHA256$iterations$salt$hash</c>), so the iteration count can be raised later
/// and old hashes still verify with the count they were created with. Hash and Verify cost
/// the same, which Login relies on to equalize unknown-email timing.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 600_000;

    private const string Scheme = "PBKDF2-SHA256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher(int iterations = DefaultIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, _iterations);

        return $"{Scheme}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);

        var parts = passwordHash?.Split('$');

        if (parts is not { Length: 4 }
            || parts[0] != Scheme
            || !int.TryParse(parts[1], out var iterations)
            || iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            return CryptographicOperations.FixedTimeEquals(Derive(password, salt, iterations), expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
