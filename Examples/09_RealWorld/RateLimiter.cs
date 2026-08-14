using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.RealWorld;

/// <summary>
/// A minimal async token-bucket rate limiter: tokens refill at a fixed rate, callers
/// asynchronously wait for a token instead of being blocked or dropped. This is the same
/// idea behind System.Threading.RateLimiting, hand-rolled here so the mechanism is visible.
/// </summary>
sealed class RateLimiter : IAsyncExample
{
    public string Category => "09. Real World";
    public string Title => "A hand-rolled async token-bucket rate limiter";
    public string Summary => "Caps calls to 2 per second using an async token bucket, so bursts queue up and drain smoothly instead of overwhelming a downstream service.";

    public async Task RunAsync(CancellationToken ct)
    {
        using var limiter = new TokenBucket(capacity: 2, refillPerSecond: 2, ct);

        Log.Section("Firing 6 requests immediately — the limiter smooths them to ~2/sec");
        var calls = Enumerable.Range(1, 6).Select(async i =>
        {
            await limiter.WaitForTokenAsync();
            Log.Write($"request {i} allowed through");
        });

        await Task.WhenAll(calls);
        Log.Write("Notice the timestamps cluster in pairs roughly a second apart, not all at once.", ConsoleColor.Cyan);
    }

    /// <summary>Async token bucket: a background timer refills tokens; waiters queue on a SemaphoreSlim.</summary>
    private sealed class TokenBucket : IDisposable
    {
        private readonly SemaphoreSlim _tokens;
        private readonly Timer _refillTimer;
        private readonly int _capacity;

        public TokenBucket(int capacity, int refillPerSecond, CancellationToken ct)
        {
            _capacity = capacity;
            _tokens = new SemaphoreSlim(capacity, capacity);

            var interval = TimeSpan.FromSeconds(1.0 / refillPerSecond);
            _refillTimer = new Timer(_ => Refill(), null, interval, interval);
            ct.Register(Dispose);
        }

        public Task WaitForTokenAsync() => _tokens.WaitAsync();

        private void Refill()
        {
            // Never exceed capacity — CurrentCount can't be read-then-conditionally-released
            // atomically with SemaphoreSlim, so just try to release and swallow the (rare,
            // benign) case where a concurrent refill already topped it off.
            if (_tokens.CurrentCount < _capacity)
            {
                try { _tokens.Release(); }
                catch (SemaphoreFullException) { /* another refill won the race; fine to ignore */ }
            }
        }

        public void Dispose()
        {
            _refillTimer.Dispose();
            _tokens.Dispose();
        }
    }
}
