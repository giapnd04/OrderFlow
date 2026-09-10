namespace OrderFlow.Application.Abstractions.Messaging;

/// <summary>
/// Marker for an application command — a request that changes state and returns
/// <typeparamref name="TResult"/>. Commands are plain data; the matching
/// <see cref="ICommandHandler{TCommand,TResult}"/> holds the behaviour.
/// </summary>
public interface ICommand<TResult>
{
}

/// <summary>
/// Handles exactly one <see cref="ICommand{TResult}"/> type. This is the CQRS write
/// seam for OrderFlow — deliberately a plain contract, not a mediator/dispatcher
/// (SDS §18, §23.6: no mediator abstraction without a concrete need).
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken = default);
}
