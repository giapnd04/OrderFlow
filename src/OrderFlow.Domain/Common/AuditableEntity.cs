namespace OrderFlow.Domain.Common;

/// <summary>
/// Entity that carries created_at / updated_at timestamps (schema v1, §11).
/// Values are stored in UTC. They are maintained by the persistence layer
/// (SaveChanges interceptor + database SYSUTCDATETIME() defaults), not by domain code.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
