namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Runs several repository calls atomically. Repositories each persist on their own, so
/// a use case that must write more than one aggregate (e.g. Register creates a Customer,
/// a User and an OTP) wraps them here: all commit, or none do.
/// </summary>
public interface ITransactionRunner
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
