namespace OrderFlow.Domain.Enums;

/// <summary>
/// Role of an authentication identity (ADR-002). Persisted as nvarchar with a DB CHECK constraint.
/// </summary>
public enum UserRole
{
    Customer,
    Sales,
    Warehouse,
    Administrator
}
