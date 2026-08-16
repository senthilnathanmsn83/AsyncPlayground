using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.RealWorld;

/// <summary>
/// Combines several techniques from earlier examples into one realistic scenario:
/// download N files concurrently (bounded), report progress via IProgress&lt;T&gt;
/// (which marshals callbacks safely regardless of which thread reports them), and
/// support cancelling the whole batch cleanly.
/// </summary>
sealed class SimulatedDownloadsWithProgress : IAsyncExample
{
    public string Category => "09. Real World";
    public string Title => "Concurrent downloads with progress reporting and cancellation";
    public string Summary => "Downloads 6 files with max 3 concurrent, reports overall progress via IProgress<T>, and can be cancelled mid-batch.";

    public async Task RunAsync(CancellationToken ct)
    {
        var files = Enumerable.Range(1, 6).Select(i => $"file-{i}.zip").ToArray();
        int completedCount = 0;

        // IProgress<T>.Report captures the SynchronizationContext at construction time (if
        // any) and marshals back to it — the same mechanism `await` uses. In a console app
        // there's no context to marshal to, so this just runs the callback directly, but in
        // a UI app this is what lets you safely update a progress bar from background work.
        var progress = new Progress<(string file, int completed, int total)>(p =>
            Log.Write($"progress: {p.completed}/{p.total} done (just finished {p.file})", ConsoleColor.Green));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // Uncomment to see cancellation mid-batch instead of letting it finish:
        // cts.CancelAfter(300);

        try
        {
            await Parallel.ForEachAsync(
                files,
                new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cts.Token },
                async (file, innerCt) =>
                {
                    await Sim.FetchAsync(file, 200, innerCt);
                    int done = Interlocked.Increment(ref completedCount);
                    ((IProgress<(string, int, int)>)progress).Report((file, done, files.Length));
                });

            Log.Write("all downloads complete", ConsoleColor.Cyan);
        }
        catch (OperationCanceledException)
        {
            Log.Write($"batch cancelled after {completedCount}/{files.Length} completed — the rest were abandoned cleanly", ConsoleColor.Yellow);
        }
    }
}
