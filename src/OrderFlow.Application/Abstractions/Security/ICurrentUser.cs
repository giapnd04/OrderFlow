using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Abstractions.Security;

/// <summary>
/// The authenticated caller, as seen by the application layer (ADR-002). Customer-facing
/// endpoints compare <see cref="CustomerId"/> against the owner of the resource; staff
/// roles bypass that ownership check.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    UserRole? Role { get; }

    /// <summary>Set only for Customer-role users whose account is linked to a customer record.</summary>
    int? CustomerId { get; }
}
