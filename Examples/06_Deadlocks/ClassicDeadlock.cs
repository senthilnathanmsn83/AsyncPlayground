using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Deadlocks;

/// <summary>
/// The single most common production async bug: calling .Result or .Wait() on a Task
/// from a thread that owns a single-threaded SynchronizationContext (WPF, WinForms,
/// classic ASP.NET). The blocking call occupies that thread; the awaited method's
/// continuation is posted BACK to that same thread's context to resume; nobody is left
/// to run it. Both sides wait forever. Plain console apps have no such context, so this
/// doesn't normally happen here — this example installs one on purpose to show it safely.
/// </summary>
sealed class ClassicDeadlock : IAsyncExample
{
    public string Category => "06. Deadlocks & Context";
    public string Title => "The classic sync-over-async deadlock (and the fix)";
    public string Summary => "Recreates the '.Result deadlock' from WPF/WinForms/classic ASP.NET using a single-threaded context, then fixes it with ConfigureAwait(false).";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Why plain console apps usually don't deadlock like this");
        Log.Write("No SynchronizationContext is installed by default, so a continuation can resume on any thread-pool thread — blocking one thread doesn't starve it.");

        Log.Section("Reproducing it: blocking with .Result on a single-threaded context");
        bool deadlocked = BlockAndDetectDeadlock(configureAwaitFalse: false);
        Log.Write(
            deadlocked
                ? "DEADLOCK CONFIRMED: the context's thread is blocked on .Result, and the continuation it's waiting for is stuck in the queue behind that same block."
                : "did not deadlock (unexpected)",
            deadlocked ? ConsoleColor.Red : ConsoleColor.Yellow);

        Log.Section("The fix: ConfigureAwait(false) — don't capture the context, don't need it back");
        bool deadlockedWithFix = BlockAndDetectDeadlock(configureAwaitFalse: true);
        Log.Write(
            deadlockedWithFix
                ? "still deadlocked (unexpected)"
                : "completed fine — the continuation ran on a thread-pool thread and never needed the blocked thread.",
            deadlockedWithFix ? ConsoleColor.Red : ConsoleColor.Green);

        Log.Write("Better fix than ConfigureAwait(false): don't block at all — 'async all the way up' (await in the caller instead of .Result/.Wait()).", ConsoleColor.Cyan);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Runs the blocking call on a dedicated background thread so that if it genuinely
    /// deadlocks, it only leaks that one thread instead of hanging this entire demo app.
    /// A 2-second join timeout is how we detect "it's stuck" without waiting forever.
    /// </summary>
    private static bool BlockAndDetectDeadlock(bool configureAwaitFalse)
    {
        var pump = new SingleThreadSyncContext();
        var thread = new Thread(() =>
        {
            pump.RunBlocking(() =>
            {
                Task<string> task = configureAwaitFalse
                    ? FetchWithConfigureAwaitFalseAsync()
                    : FetchAsync();

                string result = task.Result; // <-- the classic sync-over-async blocking call
                Log.Write($"got: {result}", ConsoleColor.Green);
            });
        })
        {
            IsBackground = true, // let the process still exit even if this thread stays stuck forever
        };
        thread.Start();

        bool finishedInTime = thread.Join(TimeSpan.FromSeconds(2));
        return !finishedInTime;
    }

    private static async Task<string> FetchAsync()
    {
        // No ConfigureAwait(false): this await captures the current SynchronizationContext
        // and will try to resume back on it — the thread that's currently blocked on .Result.
        await Task.Delay(200);
        return "captured-context-result";
    }

    private static async Task<string> FetchWithConfigureAwaitFalseAsync()
    {
        // ConfigureAwait(false): resume on a thread-pool thread instead of the captured
        // context, so the resumption never needs the blocked thread to be free.
        await Task.Delay(200).ConfigureAwait(false);
        return "configure-await-false-result";
    }
}
