using System.Net.Mail;

namespace OrderFlow.Domain.Common;

/// <summary>
/// Single definition of "a valid email address" shared by <c>Customer</c> and <c>User</c>,
/// so the two entities can never disagree about what an acceptable email is.
/// </summary>
public static class EmailRules
{
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();

        try
        {
            var address = new MailAddress(trimmed);

            return address.Address.Equals(trimmed, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
