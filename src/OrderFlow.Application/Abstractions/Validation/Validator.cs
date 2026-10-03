namespace OrderFlow.Application.Abstractions.Validation;

/// <summary>Base class: implement <see cref="Check"/> and report problems through the collector.</summary>
public abstract class Validator<T> : IValidator<T>
{
    public IReadOnlyList<ValidationFailure> Validate(T instance)
    {
        var errors = new ErrorCollector();

        Check(instance, errors);

        return errors.Failures;
    }

    protected abstract void Check(T instance, ErrorCollector errors);
}

public sealed class ErrorCollector
{
    private readonly List<ValidationFailure> _failures = new();

    public IReadOnlyList<ValidationFailure> Failures => _failures;

    public void Add(string property, string message) => _failures.Add(new ValidationFailure(property, message));

    public void AddIf(bool condition, string property, string message)
    {
        if (condition)
        {
            Add(property, message);
        }
    }
}
