using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Exceptions;

/// <summary>
/// Exceptions in async code behave mostly like sync code — try/catch around an `await`
/// works exactly as expected. The surprising part is Task.WhenAll: it aggregates every
/// failure into an AggregateException, but re-throws only the FIRST one when awaited
/// directly, so the others are silently swallowed unless you inspect Task.Exception.
/// </summary>
sealed class ExceptionHandling : IAsyncExample
{
    public string Category => "03. Exceptions";
    public string Title => "Exception propagation, and the WhenAll gotcha";
    public string Summary => "try/catch works normally across await, but awaiting a failed Task.WhenAll only surfaces the FIRST exception.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Straightforward: try/catch around an await");
        try
        {
            await Sim.FlakyFetchAsync("payments-api", 100, failProbability: 1.0, ct);
        }
        catch (HttpRequestLikeException ex)
        {
            Log.Write($"caught: {ex.Message}", ConsoleColor.Green);
        }

        Log.Section("Task.WhenAll with multiple failures: only the first exception propagates from 'await'");
        Task<string> t1 = FailAsync("service-A", 50);
        Task<string> t2 = FailAsync("service-B", 100);
        Task<string> t3 = Sim.FetchAsync("service-C", 75, ct);

        Task allTask = Task.WhenAll(t1, t2, t3);
        try
        {
            await allTask;
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"'await' surfaced only ONE exception: {ex.Message}", ConsoleColor.Yellow);
        }

        // The fix: inspect the combined Task's .Exception (an AggregateException) to see everything.
        if (allTask.Exception is { } aggregate)
        {
            Log.Write($"but the Task's .Exception has all {aggregate.InnerExceptions.Count}:", ConsoleColor.Green);
            foreach (var inner in aggregate.InnerExceptions)
                Log.Write($"  - {inner.Message}", ConsoleColor.Green);
        }

        Log.Write("Rule: when you need every failure from a WhenAll, inspect task.Exception, don't rely on the awaited exception alone.", ConsoleColor.Cyan);
    }

    private static async Task<string> FailAsync(string name, int delayMs)
    {
        await Task.Delay(delayMs);
        throw new InvalidOperationException($"'{name}' failed");
    }
}
