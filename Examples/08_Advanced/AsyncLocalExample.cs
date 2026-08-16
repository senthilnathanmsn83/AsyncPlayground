using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Advanced;

/// <summary>
/// Ordinary [ThreadStatic] fields break under async code, because a single logical
/// operation can hop across many different threads as it resumes after each await.
/// AsyncLocal&lt;T&gt; flows its value with the logical call context instead — this is how
/// ILogger scopes and Activity/trace IDs survive across awaits in ASP.NET Core.
/// </summary>
sealed class AsyncLocalExample : IAsyncExample
{
    private static readonly AsyncLocal<string?> CorrelationId = new();

    public string Category => "08. Advanced";
    public string Title => "AsyncLocal<T>: context that survives thread hops";
    public string Summary => "Flows a 'correlation id' through an async call chain that hops threads at every await, the same mechanism behind logging scopes.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Two concurrent logical operations, each with its own correlation id");
        Task requestA = HandleRequestAsync("req-A", ct);
        Task requestB = HandleRequestAsync("req-B", ct);
        await Task.WhenAll(requestA, requestB);

        Log.Write("Each operation saw only its own id at every step, even though threads were shared/reused.", ConsoleColor.Cyan);
        Log.Write("A plain static or [ThreadStatic] field would have leaked one request's id into the other's logs.", ConsoleColor.Cyan);
    }

    private static async Task HandleRequestAsync(string correlationId, CancellationToken ct)
    {
        CorrelationId.Value = correlationId; // setting it here scopes it to this logical call and its children
        Log.Write($"[{CorrelationId.Value}] starting on this thread");

        await Sim.FetchAsync($"{correlationId}-step-1", 120, ct);
        Log.Write($"[{CorrelationId.Value}] resumed after step 1 — id followed us across the thread hop");

        await DoNestedWorkAsync(ct);

        await Sim.FetchAsync($"{correlationId}-step-2", 80, ct);
        Log.Write($"[{CorrelationId.Value}] resumed after step 2, still correct");
    }

    private static async Task DoNestedWorkAsync(CancellationToken ct)
    {
        // No parameter passing needed — AsyncLocal.Value is visible here even though
        // this method was never told which "request" it's part of.
        Log.Write($"[{CorrelationId.Value}] nested call sees the same id with no explicit parameter");
        await Task.Delay(50, ct);
    }
}
