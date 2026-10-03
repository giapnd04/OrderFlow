using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Wraps repository calls in one database transaction. Every repository shares this scoped
/// <see cref="OrderFlowDbContext"/>, so their individual SaveChanges calls all join the
/// ambient transaction and commit or roll back together.
/// </summary>
internal sealed class EfTransactionRunner : ITransactionRunner
{
    private readonly OrderFlowDbContext _db;

    public EfTransactionRunner(OrderFlowDbContext db) => _db = db;

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            await action(cancellationToken);

            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await action(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
