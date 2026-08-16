using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Streaming;

/// <summary>
/// IAsyncEnumerable&lt;T&gt; + `await foreach` lets you stream items as they become
/// available instead of waiting for an entire collection to be ready — useful for paged
/// API results, large query results, or any producer that yields values over time.
/// </summary>
sealed class AsyncEnumerableBasics : IAsyncExample
{
    public string Category => "07. Streaming";
    public string Title => "IAsyncEnumerable<T> and await foreach";
    public string Summary => "Streams simulated 'pages' of results one at a time, processing each as soon as it arrives, with cancellation support built into the stream.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Consuming a stream with await foreach");
        await foreach (string page in FetchPagesAsync(totalPages: 4, ct))
        {
            Log.Write($"processing {page}");
        }

        Log.Section("Cancelling mid-stream");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(250); // cancel partway through a 4 x 150ms stream
        try
        {
            await foreach (string page in FetchPagesAsync(totalPages: 4, cts.Token))
            {
                Log.Write($"processing {page}");
            }
        }
        catch (OperationCanceledException)
        {
            Log.Write("stream cancelled partway through — WithCancellation propagated the token into the enumerator", ConsoleColor.Yellow);
        }
    }

    /// <summary>
    /// An `async IEnumerable&lt;T&gt;` method: `yield return` inside an `async` method
    /// produces a lazily-evaluated, awaitable stream. Nothing here runs until the
    /// consumer starts enumerating with `await foreach`.
    /// </summary>
    private static async IAsyncEnumerable<string> FetchPagesAsync(
        int totalPages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int page = 1; page <= totalPages; page++)
        {
            await Task.Delay(150, ct); // simulates fetching the next page over the network
            yield return $"page-{page}/{totalPages}";
        }
    }
}
