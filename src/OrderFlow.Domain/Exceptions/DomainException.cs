namespace OrderFlow.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would put a domain entity into a state that violates
/// one of its invariants (schema v1, §12.3). Represents a rule that must always
/// hold regardless of how the system is called.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
