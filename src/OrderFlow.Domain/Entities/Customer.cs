using OrderFlow.Domain.Common;
using OrderFlow.Domain.Exceptions;
using System.Net.Mail;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Customer that owns orders (schema v1, §1). email is unique.
/// </summary>
public class Customer : AuditableEntity
{
    public string Name { get;private set; } = null!;

    public string Email { get;private set; } = null!;

    public string? Phone { get;private set; }

    public static Customer Create(
        string name,
        string email,
        string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Customer requires a name.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Customer requires an email.");
        }

        if (!IsValidEmail(email))
        {
            throw new DomainException("Customer email is not valid.");
        }

        return new Customer
        {
            Name = name.Trim(),
            Email = email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone)
                ? null
                : phone.Trim(),
        };
    }

    /// <summary>
    /// Updates the customer's editable profile fields. Email is intentionally NOT
    /// updatable here — changing the unique identity used for login/lookup is a
    /// separate concern (verification, re-auth) left out of v1 scope.
    /// </summary>
    public void UpdateProfile(string name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Customer requires a name.");
        }

        Name = name.Trim();
        Phone = string.IsNullOrWhiteSpace(phone)
            ? null
            : phone.Trim();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email.Trim());

            return address.Address.Equals(
                email.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
