using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Authentication identity (ADR-002). Deliberately separate from <see cref="Customer"/>:
/// a Customer is a business record that owns orders and may never log in; a User is a
/// login that may (Role = Customer) be linked to one Customer. The password hash is
/// produced by the application layer's hasher — the domain never sees a plain password.
/// </summary>
public class User : AuditableEntity
{
    // EF Core materialisation constructor.
    private User()
    {
    }

    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public int? CustomerId { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public static User Create(
        string email,
        string passwordHash,
        UserRole role,
        bool isEmailVerified = false)
    {
        if (!EmailRules.IsValid(email))
        {
            throw new DomainException("User email is not valid.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("User requires a password hash.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new DomainException("User role is not valid.");
        }

        return new User
        {
            Email = Normalize(email),
            PasswordHash = passwordHash,
            Role = role,
            IsEmailVerified = isEmailVerified,
        };
    }

    /// <summary>Emails are compared case-insensitively everywhere, so they are stored lower-cased.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Links this login to its business <see cref="Customer"/>. Only Customer-role users
    /// are linkable, and a user is linked at most once.
    /// </summary>
    public void LinkCustomer(int customerId)
    {
        if (Role != UserRole.Customer)
        {
            throw new DomainException("Only customer accounts can be linked to a customer record.");
        }

        if (customerId <= 0)
        {
            throw new DomainException("Customer ID must be greater than zero.");
        }

        if (CustomerId is not null && CustomerId != customerId)
        {
            throw new DomainException("User is already linked to a different customer.");
        }

        CustomerId = customerId;
    }

    /// <summary>Idempotent: verifying an already verified email is a no-op.</summary>
    public void MarkEmailVerified()
    {
        IsEmailVerified = true;
    }
}
