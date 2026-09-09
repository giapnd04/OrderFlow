namespace OrderFlow.Domain.Common;

/// <summary>
/// Base type for persistent entities. All OrderFlow tables use an int IDENTITY primary key (schema v1, §7).
/// </summary>
public abstract class Entity
{
    public int Id { get; set; }
}
