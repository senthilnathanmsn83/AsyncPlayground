using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Synchronization;

/// <summary>
/// SemaphoreSlim is the standard way to cap concurrency in async code — e.g. "call this
/// API for 20 items, but never more than 3 at once". Unlike `lock`, SemaphoreSlim has a
/// WaitAsync that doesn't block a thread while waiting for a slot to free up.
/// </summary>
sealed class SemaphoreThrottling : IAsyncExample
{
    public string Category => "05. Synchronization";
    public string Title => "Throttling concurrency with SemaphoreSlim";
    public string Summary => "Runs 8 simulated calls with at most 3 in flight at a time, using SemaphoreSlim.WaitAsync/Release.";

    public async Task RunAsync(CancellationToken ct)
    {
        const int maxConcurrent = 3;
        using var gate = new SemaphoreSlim(initialCount: maxConcurrent, maxCount: maxConcurrent);

        Log.Section($"8 items, throttled to {maxConcurrent} concurrent");
        var items = Enumerable.Range(1, 8);
        var tasks = items.Select(i => ProcessOneAsync(i, gate, ct));
        await Task.WhenAll(tasks);

        Log.Write("Watch the timestamps above: never more than 3 'start' lines appear before a 'done' frees a slot.", ConsoleColor.Cyan);
    }

    private static async Task ProcessOneAsync(int id, SemaphoreSlim gate, CancellationToken ct)
    {
        await gate.WaitAsync(ct); // waits asynchronously for a free slot — no thread is blocked here
        try
        {
            await Sim.FetchAsync($"item-{id}", 200, ct);
        }
        finally
        {
            gate.Release(); // always release in a finally, even if the work above throws or is cancelled
        }
    }
}
