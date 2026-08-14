using System.Diagnostics;
using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Composition;

/// <summary>
/// The single highest-leverage async mistake: awaiting independent operations one at a
/// time instead of starting them together. This shows both, with a stopwatch, so the
/// difference isn't just theoretical.
/// </summary>
sealed class SequentialVsConcurrent : IAsyncExample
{
    public string Category => "02. Composition";
    public string Title => "Sequential awaits vs starting work concurrently";
    public string Summary => "Three 300ms 'calls' take ~900ms awaited one-by-one, but ~300ms started together and awaited with Task.WhenAll.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Sequential: await each call before starting the next");
        var sw = Stopwatch.StartNew();
        var a = await Sim.FetchAsync("users", 300, ct);
        var b = await Sim.FetchAsync("orders", 300, ct);
        var c = await Sim.FetchAsync("products", 300, ct);
        sw.Stop();
        Log.Write($"sequential total: {sw.ElapsedMilliseconds}ms for [{a}, {b}, {c}]", ConsoleColor.Yellow);

        Log.Section("Concurrent: start all three, THEN await");
        sw.Restart();
        // Calling the async method without awaiting starts it immediately and returns a
        // hot Task. Doing this three times before awaiting anything is what lets the
        // three simulated calls run in overlapping time.
        Task<string> taskA = Sim.FetchAsync("users", 300, ct);
        Task<string> taskB = Sim.FetchAsync("orders", 300, ct);
        Task<string> taskC = Sim.FetchAsync("products", 300, ct);

        string[] results = await Task.WhenAll(taskA, taskB, taskC);
        sw.Stop();
        Log.Write($"concurrent total: {sw.ElapsedMilliseconds}ms for [{string.Join(", ", results)}]", ConsoleColor.Green);

        Log.Write("Rule of thumb: if operations don't depend on each other's results, start them all before awaiting any.", ConsoleColor.Cyan);
    }
}
