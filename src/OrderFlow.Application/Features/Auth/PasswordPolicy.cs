using OrderFlow.Application.Abstractions.Validation;

namespace OrderFlow.Application.Features.Auth;

/// <summary>
/// Password rules shared by every use case that sets a password. The upper bound exists
/// because PBKDF2 cost grows with input length - an unbounded password is a cheap DoS.
/// </summary>
internal static class PasswordPolicy
{
    public const int MinLength = 8;

    public const int MaxLength = 128;

    public static void Check(string? password, string property, ErrorCollector errors)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors.Add(property, "Password is required.");
            return;
        }

        errors.AddIf(password.Length < MinLength, property, $"Password must be at least {MinLength} characters.");
        errors.AddIf(password.Length > MaxLength, property, $"Password must be at most {MaxLength} characters.");
        errors.AddIf(!password.Any(char.IsLetter), property, "Password must contain at least one letter.");
        errors.AddIf(!password.Any(char.IsDigit), property, "Password must contain at least one digit.");
    }
}
