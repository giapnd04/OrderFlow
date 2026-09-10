namespace OrderFlow.Application.Exceptions;

/// <summary>
/// The request is structurally or business-rule invalid (SDS §12, §14 — maps to
/// HTTP 400 at the API boundary).
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
    }
}
