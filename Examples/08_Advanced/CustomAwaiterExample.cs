using System.Runtime.CompilerServices;
using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Advanced;

/// <summary>
/// `await` is not magic tied to Task — it's a compiler pattern. Any type with a
/// GetAwaiter() method returning something with IsCompleted, OnCompleted(Action), and
/// GetResult() can be awaited. This builds the minimum awaitable type from scratch to
/// demystify what `await someTask` actually compiles down to.
/// </summary>
sealed class CustomAwaiterExample : IAsyncExample
{
    public string Category => "08. Advanced";
    public string Title => "Building a custom awaitable from scratch";
    public string Summary => "await isn't special-cased to Task — it's a duck-typed compiler pattern. Implements the minimum shape needed to make a custom type awaitable.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Awaiting our own type, not Task<T>");
        int result = await new DelayedValue(200, 42);
        Log.Write($"awaited a hand-rolled awaitable and got: {result}", ConsoleColor.Green);

        Log.Write("The compiler only needs: GetAwaiter(), bool IsCompleted, void OnCompleted(Action), T GetResult().", ConsoleColor.Cyan);
        Log.Write("Task<T>, ValueTask<T>, and this custom type all satisfy that same shape.", ConsoleColor.Cyan);
        await Task.CompletedTask;
    }

    // Rooted for the lifetime of this example so the timer below can't be collected
    // before it fires — System.Threading.Timer is not self-rooting.
    private static System.Threading.Timer? _pendingTimer;

    /// <summary>An awaitable that isn't a Task at all — just a type following the awaiter pattern.</summary>
    private readonly struct DelayedValue(int delayMs, int value)
    {
        public DelayedValueAwaiter GetAwaiter() => new(delayMs, value);
    }

    /// <summary>
    /// The minimum viable awaiter. Real awaiters (Task's included) also implement
    /// INotifyCompletion/ICriticalNotifyCompletion so the compiler can hook up
    /// continuations without allocating extra state where possible.
    /// </summary>
    private readonly struct DelayedValueAwaiter(int delayMs, int value) : INotifyCompletion
    {
        // Always false here: this example always suspends so OnCompleted's path runs
        // and is visible. A real awaiter would check whether the result is already ready.
        public bool IsCompleted => false;

        public void OnCompleted(Action continuation)
        {
            Log.Write($"OnCompleted called — scheduling continuation after {delayMs}ms via a timer, no thread blocked");
            _pendingTimer = new System.Threading.Timer(_ =>
            {
                _pendingTimer?.Dispose();
                continuation();
            }, null, delayMs, Timeout.Infinite);
        }

        public int GetResult()
        {
            Log.Write("GetResult called — this is what 'await' evaluates to");
            return value;
        }
    }
}
