using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OrderFlow.IntegrationTests.Fixtures;

public sealed class InjectedFaultException(string message) : Exception(message);

// Fails the n-th SaveChanges before it sends any SQL: "the second write of a use case crashed".
public sealed class FailOnSaveChangesInterceptor(int failOnCall) : SaveChangesInterceptor
{
    private int _calls;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (++_calls == failOnCall)
        {
            throw new InjectedFaultException($"Injected failure on SaveChanges call #{failOnCall}.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

// Fails the first SQL command containing sqlFragment; earlier commands of the same
// SaveChanges have already run, so this is a failure half-way through one save.
public sealed class FailOnCommandInterceptor(string sqlFragment) : DbCommandInterceptor
{
    private bool _failed;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (!_failed && command.CommandText.Contains(sqlFragment, StringComparison.OrdinalIgnoreCase))
        {
            _failed = true;
            throw new InjectedFaultException($"Injected failure on SQL containing '{sqlFragment}'.");
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
