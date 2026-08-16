using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Advanced;

/// <summary>
/// Parallel.ForEachAsync is the built-in replacement for "SemaphoreSlim + Select +
/// WhenAll" when you just need to run an async body over a collection with bounded
/// concurrency. It also wires up cancellation for you automatically.
/// </summary>
sealed class ParallelForEachAsyncExample : IAsyncExample
{
    public string Category => "08. Advanced";
    public string Title => "Parallel.ForEachAsync for bounded-concurrency loops";
    public string Summary => "The built-in, cancellation-aware alternative to hand-rolling SemaphoreSlim + WhenAll for 'do this async thing to every item, N at a time'.";

    public async Task RunAsync(CancellationToken ct)
    {
        var orderIds = Enumerable.Range(1, 10).ToArray();

        Log.Section("Processing 10 orders, at most 4 concurrently");
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(orderIds, options, async (orderId, innerCt) =>
        {
            await Sim.FetchAsync($"order-{orderId}", 150, innerCt);
        });

        Log.Write("All orders processed. Compare this to Examples/05_Synchronization/SemaphoreThrottling.cs —", ConsoleColor.Cyan);
        Log.Write("same effect, far less boilerplate, when you don't need custom queueing logic.", ConsoleColor.Cyan);
    }
}
