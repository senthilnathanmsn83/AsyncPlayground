using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Cancellation;

/// <summary>
/// Two idiomatic ways to bound how long an await can take: linking a timeout
/// CancellationTokenSource to an existing token, and the newer Task.WaitAsync(timeout)
/// helper. Both stop *waiting* — neither one magically stops the underlying work unless
/// that work itself observes the same token.
/// </summary>
sealed class TimeoutPatterns : IAsyncExample
{
    public string Category => "04. Cancellation";
    public string Title => "Timeout patterns: linked tokens and Task.WaitAsync";
    public string Summary => "How to bound an await with a timeout while still respecting an incoming CancellationToken, two ways.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Pattern 1: CreateLinkedTokenSource + CancelAfter");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromMilliseconds(200));
        try
        {
            var result = await Sim.FetchAsync("slow-partner-api", 800, linked.Token);
            Log.Write($"got {result} (would not print — call was slower than the timeout)");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The `when` filter distinguishes "our timeout fired" from "the caller's
            // token was cancelled" — both throw OperationCanceledException, but only
            // one of them means "timed out" rather than "the caller gave up".
            Log.Write("timed out via linked CancellationTokenSource (caller's token was still fine)", ConsoleColor.Yellow);
        }

        Log.Section("Pattern 2: Task.WaitAsync(TimeSpan) — no manual token plumbing needed");
        try
        {
            var task = Sim.FetchAsync("another-slow-api", 800, ct);
            var result = await task.WaitAsync(TimeSpan.FromMilliseconds(200), ct);
            Log.Write($"got {result} (would not print)");
        }
        catch (TimeoutException)
        {
            // WaitAsync throws TimeoutException, not OperationCanceledException — a
            // useful distinction if you want to log timeouts differently from cancellations.
            Log.Write("timed out via Task.WaitAsync — note it throws TimeoutException, not OperationCanceledException", ConsoleColor.Yellow);
        }

        Log.Write("Important: pattern 1 passed the linked token INTO the call, so the delay itself was cancelled.", ConsoleColor.Cyan);
        Log.Write("Pattern 2's WaitAsync only stopped waiting — the inner task got the plain 'ct', not a timeout-linked", ConsoleColor.Cyan);
        Log.Write("token, so it kept running in the background. WaitAsync alone never cancels the underlying work.", ConsoleColor.Cyan);
    }
}
