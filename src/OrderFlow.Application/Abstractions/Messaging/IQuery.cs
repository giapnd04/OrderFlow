namespace OrderFlow.Application.Abstractions.Messaging;

/// <summary>
/// Marker for an application query — a request that reads state without changing it
/// and returns <typeparamref name="TResult"/>. Mirrors <see cref="ICommand{TResult}"/>
/// but on the read side, so the name of a handler tells you whether it is safe to call
/// repeatedly with no side effects.
/// </summary>
public interface IQuery<TResult>
{
}

/// <summary>
/// Handles exactly one <see cref="IQuery{TResult}"/> type. This is the CQRS read seam
/// for OrderFlow — same shape as <see cref="ICommandHandler{TCommand,TResult}"/>,
/// deliberately not a mediator/dispatcher (SDS §18, §23.6).
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default);
}
