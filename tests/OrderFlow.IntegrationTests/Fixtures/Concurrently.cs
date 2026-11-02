namespace OrderFlow.IntegrationTests.Fixtures;

public static class Concurrently
{
    // Holds every call at the same starting line, then releases them together so they overlap
    // in the database instead of running one after another.
    public static async Task<T[]> RunAsync<T>(params Func<Task<T>>[] calls)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = calls.Select(async call =>
        {
            await start.Task;
            return await call();
        }).ToList();

        start.SetResult();

        return await Task.WhenAll(tasks);
    }

    public static Task<T[]> RunAsync<T>(int count, Func<Task<T>> call)
        => RunAsync(Enumerable.Repeat(call, count).ToArray());
}
