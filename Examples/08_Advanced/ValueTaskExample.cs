using System.Diagnostics;
using System.Threading.Tasks.Sources;
using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Advanced;

/// <summary>
/// ValueTask&lt;T&gt; exists to avoid allocating a Task object on the hot path where a
/// result is very often already available synchronously (e.g. cache hits). It comes with
/// sharp edges: a ValueTask must be awaited at most once and never awaited concurrently
/// from two places, unlike Task, which tolerates both.
/// </summary>
sealed class ValueTaskExample : IAsyncExample
{
    public string Category => "08. Advanced";
    public string Title => "ValueTask<T>: cheap synchronous completion, sharp edges";
    public string Summary => "Shows why ValueTask<T> avoids allocations on cache hits, and the 'await it exactly once' rule that comes with that.";

    private readonly Dictionary<string, int> _cache = new();

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("First call: cache miss, goes through the (simulated) slow path");
        int a = await GetValueAsync("key-1", ct);
        Log.Write($"got {a}");

        Log.Section("Second call, same key: cache hit — completes synchronously, no Task allocated");
        var sw = Stopwatch.StartNew();
        int b = await GetValueAsync("key-1", ct);
        sw.Stop();
        Log.Write($"got {b} in {sw.Elapsed.TotalMicroseconds:F0}us (no await suspension needed)", ConsoleColor.Green);

        Log.Section("The rule ValueTask enforces that Task doesn't: await it exactly once");
        Log.Write("(A ValueTask<T> backed by a plain Task tolerates a second await harmlessly — the real hazard");
        Log.Write(" is a pooled IValueTaskSource<T>, like real high-performance libraries use. Reproducing that:");
        var source = new PooledIntSource();
        source.SetResult(99);
        ValueTask<int> vt = new(source, source.Version);

        int first = await vt;
        Log.Write($"first await: {first}");

        source.ResetForReuse(); // simulates the pool handing this backing object to someone else
        try
        {
            int second = await vt; // this ValueTask's token is now stale — GetResult(token) rejects it
            Log.Write($"second await returned {second} (should not happen)", ConsoleColor.Yellow);
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"awaiting the same pooled-source ValueTask twice threw: {ex.Message}", ConsoleColor.Red);
        }

        Log.Write("Rule: use ValueTask<T> only for hot, often-synchronous paths, and treat each instance as single-use. When in doubt, use Task<T>.", ConsoleColor.Cyan);
    }

    private ValueTask<int> GetValueAsync(string key, CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out int cached))
        {
            // Synchronous path: wrapping a plain value costs no Task allocation at all.
            return new ValueTask<int>(cached);
        }

        return new ValueTask<int>(SlowComputeAndCacheAsync(key, ct));
    }

    private async Task<int> SlowComputeAndCacheAsync(string key, CancellationToken ct)
    {
        await Task.Delay(150, ct);
        int value = key.Length * 7;
        _cache[key] = value;
        return value;
    }

    /// <summary>
    /// A minimal IValueTaskSource&lt;int&gt;, the mechanism real zero-allocation APIs
    /// (e.g. pipe/socket readers) use to back a ValueTask without a Task object. Its
    /// version token is exactly what makes "await it only once" a hard rule instead of
    /// just a guideline: once reset (as a pool would on reuse), the old token is invalid.
    /// </summary>
    private sealed class PooledIntSource : IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _core;

        public short Version => _core.Version;

        public void SetResult(int result) => _core.SetResult(result);

        public void ResetForReuse() => _core.Reset();

        public int GetResult(short token) => _core.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
    }
}
