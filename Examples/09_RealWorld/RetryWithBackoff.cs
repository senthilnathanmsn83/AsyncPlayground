using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.RealWorld;

/// <summary>
/// A hand-rolled retry-with-exponential-backoff-and-jitter helper — the pattern behind
/// libraries like Polly. Jitter matters: without it, many clients retrying on the same
/// fixed schedule after an outage all hammer the server at the same instants.
/// </summary>
sealed class RetryWithBackoff : IAsyncExample
{
    public string Category => "09. Real World";
    public string Title => "Retry with exponential backoff and jitter";
    public string Summary => "Retries a flaky call with delays that grow exponentially and include randomness (jitter), giving up after a max attempt count.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Retrying a call that fails 70% of the time, up to 5 attempts");
        try
        {
            string result = await RetryAsync(
                () => Sim.FlakyFetchAsync("unstable-service", 100, failProbability: 0.7, ct),
                maxAttempts: 5,
                ct);
            Log.Write($"eventually succeeded: {result}", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            Log.Write($"gave up after all attempts: {ex.Message}", ConsoleColor.Red);
        }
    }

    private static async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxAttempts, CancellationToken ct)
    {
        var random = new Random();
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                // Exponential: 100ms, 200ms, 400ms, 800ms... Jitter: +/- up to 50% of that,
                // so simultaneous retriers spread out instead of retrying in lockstep.
                int baseDelayMs = 100 * (int)Math.Pow(2, attempt - 1);
                int jitterMs = random.Next(-baseDelayMs / 2, baseDelayMs / 2 + 1);
                int delayMs = Math.Max(0, baseDelayMs + jitterMs);

                Log.Write($"attempt {attempt}/{maxAttempts} failed ({ex.Message}); retrying in {delayMs}ms", ConsoleColor.Yellow);
                await Task.Delay(delayMs, ct);
            }
        }

        // Final attempt: let a failure here propagate to the caller unhandled.
        return await operation();
    }
}
