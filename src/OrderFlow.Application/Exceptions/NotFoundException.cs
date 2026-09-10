namespace OrderFlow.Application.Exceptions;

/// <summary>
/// A referenced resource does not exist (SDS §14 — maps to HTTP 404 at the API boundary).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.")
    {
    }
}
