using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Validation;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Behaviors;

/// <summary>
/// Validation pipeline step (SDS §18 — a plain decorator, not a mediator). Runs every
/// registered <see cref="IValidator{T}"/> for the command *before* the inner handler, and
/// throws one <see cref="ValidationException"/> carrying all failures, so the caller sees
/// every problem at once instead of fixing them one request at a time.
/// </summary>
public sealed class ValidationCommandHandlerDecorator<TCommand, TResult> : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _inner;
    private readonly IReadOnlyList<IValidator<TCommand>> _validators;

    public ValidationCommandHandlerDecorator(
        ICommandHandler<TCommand, TResult> inner,
        IEnumerable<IValidator<TCommand>> validators)
    {
        _inner = inner;
        _validators = validators.ToList();
    }

    public Task<TResult> Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        var failures = _validators
            .SelectMany(validator => validator.Validate(command))
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return _inner.Handle(command, cancellationToken);
    }
}
