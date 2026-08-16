using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Composition;

/// <summary>
/// Task.WhenAll waits for every task and gives you all results (or throws); Task.WhenAny
/// resolves as soon as the first one finishes and leaves the rest running. Choosing the
/// wrong one is a common bug: WhenAny for a "wait for everything" case silently drops
/// work, and WhenAll for a "first response wins" race wastes time waiting on stragglers.
/// </summary>
sealed class WhenAllWhenAny : IAsyncExample
{
    public string Category => "02. Composition";
    public string Title => "Task.WhenAll vs Task.WhenAny";
    public string Summary => "WhenAll = 'need every result'. WhenAny = 'race, take the first, let the rest keep running (or cancel them)'.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("WhenAll: need every result, order doesn't matter for completion time");
        Task<string> primary = Sim.FetchAsync("primary-db", 250, ct);
        Task<string> replica = Sim.FetchAsync("replica-db", 400, ct);
        Task<string> cache = Sim.FetchAsync("cache", 100, ct);

        string[] all = await Task.WhenAll(primary, replica, cache);
        Log.Write($"all results (order matches the array, not completion order): {string.Join(", ", all)}", ConsoleColor.Green);

        Log.Section("WhenAny: racing two mirrors, first to answer wins");
        using var raceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<string> mirrorEast = Sim.FetchAsync("mirror-east", 350, raceCts.Token);
        Task<string> mirrorWest = Sim.FetchAsync("mirror-west", 150, raceCts.Token);

        Task<string> winner = await Task.WhenAny(mirrorEast, mirrorWest);
        Log.Write($"winner responded first: {await winner}", ConsoleColor.Green);

        // The loser is still running unless we cancel it. Leaving it running "leaks" work;
        // cancelling it is usually what you want in a race.
        raceCts.Cancel();
        Log.Write("cancelled the losing request instead of letting it run to completion for nothing", ConsoleColor.Yellow);

        Log.Section("Common pitfall: WhenAny does NOT observe exceptions from the losing tasks");
        var willFail = FailAfterAsync(50);
        var willSucceed = Sim.FetchAsync("slow-but-fine", 200, ct);
        var first = await Task.WhenAny(willFail, willSucceed);
        Log.Write($"WhenAny returned the failing task first, but only awaiting it throws: {first == willFail}", ConsoleColor.Yellow);
        try
        {
            await willSucceed; // still running fine in the background
            await first; // this is the one that actually throws
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"caught after explicitly awaiting the failed task: {ex.Message}", ConsoleColor.Red);
        }
    }

    private static async Task<string> FailAfterAsync(int delayMs)
    {
        await Task.Delay(delayMs);
        throw new InvalidOperationException("this task fails but WhenAny won't tell you unless you await it directly");
    }
}
