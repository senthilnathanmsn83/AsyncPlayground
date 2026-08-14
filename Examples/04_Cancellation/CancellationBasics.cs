using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Cancellation;

/// <summary>
/// CancellationToken is cooperative: nothing stops running code by force. A method has
/// to check the token (or hand it to something like Task.Delay that checks it for you)
/// and choose to unwind via OperationCanceledException. This shows the token flowing
/// through a call chain and both ways cancellation actually happens.
/// </summary>
sealed class CancellationBasics : IAsyncExample
{
    public string Category => "04. Cancellation";
    public string Title => "CancellationToken fundamentals";
    public string Summary => "Cancellation is cooperative: code must observe the token. Shows automatic cancellation (Task.Delay) and manual checks (ThrowIfCancellationRequested).";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Automatic: Task.Delay observes the token for you");
        using var cts1 = new CancellationTokenSource();
        cts1.CancelAfter(200); // simulate "user gave up waiting" after 200ms
        try
        {
            await Sim.FetchAsync("slow-report", 1000, cts1.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Write("caught OperationCanceledException — Task.Delay threw it once the token was cancelled", ConsoleColor.Yellow);
        }

        Log.Section("Manual: a CPU-bound loop must check the token itself");
        using var cts2 = new CancellationTokenSource();
        cts2.CancelAfter(150);
        try
        {
            await CountWithChecksAsync(cts2.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Write("caught OperationCanceledException from our own ThrowIfCancellationRequested() call", ConsoleColor.Yellow);
        }

        Log.Section("Cooperative means: forgetting to check the token means it's just ignored");
        using var cts3 = new CancellationTokenSource();
        cts3.CancelAfter(50);
        int completedIgnoringToken = await CountWithoutChecksAsync(cts3.Token, iterations: 5, delayPerStepMs: 40);
        Log.Write($"ran to completion anyway ({completedIgnoringToken} steps) — cancellation was requested but nothing observed it", ConsoleColor.Red);

        Log.Write("Rule: every await chain that should be cancellable must pass the token all the way down, including into loops.", ConsoleColor.Cyan);
    }

    private static async Task CountWithChecksAsync(CancellationToken ct)
    {
        for (int i = 1; i <= 10; i++)
        {
            ct.ThrowIfCancellationRequested(); // the cooperative check
            Log.Write($"step {i}/10");
            await Task.Delay(50, ct);
        }
    }

    private static async Task<int> CountWithoutChecksAsync(CancellationToken ct, int iterations, int delayPerStepMs)
    {
        int completed = 0;
        for (int i = 1; i <= iterations; i++)
        {
            // Deliberately NOT passing `ct` here and NOT calling ThrowIfCancellationRequested,
            // to show that cancellation does nothing unless code actually observes the token.
            await Task.Delay(delayPerStepMs);
            completed++;
        }
        return completed;
    }
}
