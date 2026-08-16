using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Deadlocks;

/// <summary>
/// ConfigureAwait(false) means "don't bother resuming on the context I started on — any
/// thread-pool thread is fine." This pumps a real (non-deadlocking) single-threaded
/// context to make the thread-affinity difference visible: watch the [Thread N] tag
/// before and after each await.
/// </summary>
sealed class ConfigureAwaitExplained : IAsyncExample
{
    public string Category => "06. Deadlocks & Context";
    public string Title => "ConfigureAwait(false): what it actually changes";
    public string Summary => "Runs the same async chain with and without ConfigureAwait(false) inside a single-threaded context, showing which thread each step resumes on.";

    public Task RunAsync(CancellationToken ct)
    {
        var pump = new SingleThreadSyncContext();

        Log.Section("Without ConfigureAwait(false): every step resumes on the SAME captured thread");
        pump.RunUntilComplete(() => WalkAsync(configureAwaitFalse: false, ct));

        Log.Section("With ConfigureAwait(false): steps after the first await can land on ANY thread-pool thread");
        var pump2 = new SingleThreadSyncContext();
        pump2.RunUntilComplete(() => WalkAsync(configureAwaitFalse: true, ct));

        Log.Write("Guidance: library/framework code (no UI, no request context) should generally use ConfigureAwait(false).", ConsoleColor.Cyan);
        Log.Write("App-level code that needs to get back to a UI thread (to touch controls) should NOT.", ConsoleColor.Cyan);
        return Task.CompletedTask;
    }

    private static async Task WalkAsync(bool configureAwaitFalse, CancellationToken ct)
    {
        Log.Write("before first await");

        if (configureAwaitFalse)
            await Sim.FetchAsync("step-1", 100, ct).ConfigureAwait(false);
        else
            await Sim.FetchAsync("step-1", 100, ct);

        Log.Write("after first await / before second await");

        if (configureAwaitFalse)
            await Sim.FetchAsync("step-2", 100, ct).ConfigureAwait(false);
        else
            await Sim.FetchAsync("step-2", 100, ct);

        Log.Write("after second await — compare the Thread numbers across all lines above");
    }
}
