namespace OrderFlow.Domain.Enums;

/// <summary>
/// Product lifecycle status (schema v1, §2). Persisted as nvarchar with a DB CHECK constraint.
/// </summary>
public enum ProductStatus
{
    Active,
    Discontinued
}
