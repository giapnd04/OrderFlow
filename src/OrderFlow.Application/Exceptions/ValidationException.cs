using OrderFlow.Application.Abstractions.Validation;

namespace OrderFlow.Application.Exceptions;

/// <summary>
/// The request is structurally or business-rule invalid (SDS §12, §14 — maps to
/// HTTP 400 at the API boundary). Handlers throw it with a single message; the
/// validation pipeline throws it with every failure grouped by property.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyCollection<ValidationFailure> failures)
        : base(string.Join(" ", failures.Select(f => f.Message)))
    {
        Errors = failures
            .GroupBy(f => f.Property)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Message).ToArray());
    }

    /// <summary>Messages grouped by property; empty for single-message exceptions.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
