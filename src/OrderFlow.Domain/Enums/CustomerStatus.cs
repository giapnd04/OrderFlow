namespace OrderFlow.Domain.Enums;

/// <summary>
/// Customer account status. Persisted as nvarchar with a DB CHECK constraint,
/// same pattern as <see cref="ProductStatus"/>.
/// </summary>
public enum CustomerStatus
{
    Active,
    Inactive
}
