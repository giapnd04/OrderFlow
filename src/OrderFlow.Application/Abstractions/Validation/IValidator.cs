namespace OrderFlow.Application.Abstractions.Validation;

/// <summary>
/// Validates one command's shape (required fields, formats, ranges) before its handler
/// runs. Rules that need the database or other aggregates stay in the handler.
/// </summary>
public interface IValidator<in T>
{
    IReadOnlyList<ValidationFailure> Validate(T instance);
}

public sealed record ValidationFailure(string Property, string Message);
