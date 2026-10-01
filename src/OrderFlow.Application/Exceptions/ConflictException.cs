namespace OrderFlow.Application.Exceptions;

/// <summary>
/// Application-level conflict — the request is well-formed and the resource exists,
/// but a cross-aggregate business rule blocks it (e.g. deleting a customer who still
/// has orders). Maps to 409, same bucket as the domain-level conflict exceptions
/// (<see cref="OrderFlow.Domain.Exceptions.InvalidOrderStateException"/>,
/// <see cref="OrderFlow.Domain.Exceptions.InsufficientStockException"/>) but raised by
/// a handler orchestrating repositories rather than by an entity invariant.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
