using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Customer that owns orders (schema v1, §1). email is unique.
/// </summary>
public class Customer : AuditableEntity
{
    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }
}
