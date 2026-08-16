namespace AsyncPlayground.Examples.Support;

/// <summary>
/// Stand-ins for "real" async I/O (an HTTP call, a DB query, a file read) that would
/// normally give up a thread while waiting. Using Task.Delay here is deliberate: it is
/// itself a fully asynchronous, non-blocking wait, so it behaves like real I/O for the
/// purposes of every example without needing a network or disk.
/// </summary>
static class Sim
{
    private static readonly Random Rng = new();

    public static async Task<string> FetchAsync(string name, int delayMs, CancellationToken ct = default)
    {
        Log.Write($"-> start '{name}' (~{delayMs}ms)");
        await Task.Delay(delayMs, ct);
        Log.Write($"<- done  '{name}'");
        return $"{name}-result";
    }

    public static async Task<string> FetchAsync(string name, TimeSpan delay, CancellationToken ct = default)
        => await FetchAsync(name, (int)delay.TotalMilliseconds, ct);

    public static int JitterMs(int baseMs, int spreadMs) => baseMs + Rng.Next(0, spreadMs);

    /// <summary>Simulates a flaky remote call that fails a given fraction of the time.</summary>
    public static async Task<string> FlakyFetchAsync(string name, int delayMs, double failProbability, CancellationToken ct = default)
    {
        Log.Write($"-> attempt '{name}' (~{delayMs}ms, failProb={failProbability:P0})");
        await Task.Delay(delayMs, ct);
        if (Rng.NextDouble() < failProbability)
        {
            Log.Write($"<- FAILED '{name}'", ConsoleColor.Red);
            throw new HttpRequestLikeException($"'{name}' failed (simulated transient error)");
        }

        Log.Write($"<- ok     '{name}'", ConsoleColor.Green);
        return $"{name}-result";
    }
}

/// <summary>Stand-in for a transient network exception, so examples don't need System.Net.Http.</summary>
sealed class HttpRequestLikeException(string message) : Exception(message);
