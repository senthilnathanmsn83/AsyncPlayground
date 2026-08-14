using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Basics;

/// <summary>
/// `async void` should be reserved for event handlers, because exceptions thrown inside
/// it cannot be caught by the caller with try/catch — the async state machine posts them
/// back through the current SynchronizationContext instead of propagating through a Task.
/// If nothing is listening (the default in a console app), that exception becomes
/// unhandled and crashes the process. This example installs a small custom
/// SynchronizationContext purely to observe and print that exception safely, without
/// actually taking down the demo app.
/// </summary>
sealed class AsyncVoidPitfall : IAsyncExample
{
    public string Category => "01. Basics";
    public string Title => "Why 'async void' is dangerous";
    public string Summary => "Shows that exceptions from async void cannot be try/catch'd by the caller, and where they actually go instead.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("async Task: exception IS observable by the caller");
        try
        {
            await ThrowFromTaskAsync();
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"caught normally with try/catch: {ex.Message}", ConsoleColor.Green);
        }

        Log.Section("async void: try/catch around the call site catches NOTHING");
        var capturing = new CapturingSyncContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(capturing);
        try
        {
            // No `await` is possible here — async void returns nothing to await.
            // That inability to await is itself the warning sign something is wrong.
            try
            {
                ThrowFromVoid();
                Log.Write("try/catch around the call site sees no exception at all");
            }
            catch (Exception)
            {
                Log.Write("(this never runs — the exception doesn't propagate here)", ConsoleColor.Red);
            }

            await Task.Delay(150, ct); // give the fire-and-forget method time to actually throw
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        if (capturing.Captured is { } ex2)
        {
            Log.Write($"the exception surfaced later via SynchronizationContext.Post: {ex2.Message}", ConsoleColor.Yellow);
            Log.Write("in a real console app there's no context to intercept this — it would crash the process.", ConsoleColor.Yellow);
        }

        Log.Write("Lesson: use 'async Task' everywhere except top-level UI/event handlers.", ConsoleColor.Cyan);
    }

    private static async Task ThrowFromTaskAsync()
    {
        await Task.Delay(50);
        throw new InvalidOperationException("boom from async Task");
    }

    private static async void ThrowFromVoid()
    {
        await Task.Delay(50);
        throw new InvalidOperationException("boom from async void");
    }

    /// <summary>Catches exceptions posted to it instead of letting them crash the process, purely for this demo.</summary>
    private sealed class CapturingSyncContext : SynchronizationContext
    {
        public Exception? Captured { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                Captured = ex;
            }
        }
    }
}
