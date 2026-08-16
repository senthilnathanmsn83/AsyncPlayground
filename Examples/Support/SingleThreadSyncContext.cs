using System.Collections.Concurrent;

namespace AsyncPlayground.Examples.Support;

/// <summary>
/// A minimal stand-in for the single-threaded SynchronizationContext used by WPF,
/// WinForms, and classic (System.Web) ASP.NET — every continuation posted to it runs on
/// one specific thread, one at a time. Console apps have no such context by default,
/// which is exactly why the infamous ".Result deadlock" doesn't normally happen here;
/// this recreates the mechanism on purpose so it can be observed safely.
/// </summary>
sealed class SingleThreadSyncContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    public override void Send(SendOrPostCallback d, object? state)
        => throw new NotSupportedException("Synchronous Send isn't used by these examples.");

    /// <summary>
    /// The well-behaved pattern real UI frameworks use: run <paramref name="work"/>, then
    /// pump posted continuations on THIS thread until its task completes. Because the loop
    /// keeps consuming the queue, continuations posted back to this context always get a
    /// chance to run — no deadlock.
    /// </summary>
    public void RunUntilComplete(Func<Task> work)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            Task task = work();
            task.ContinueWith(_ => _queue.CompleteAdding(), TaskScheduler.Default);

            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                callback(state);

            task.GetAwaiter().GetResult(); // observe/rethrow any exception from work()
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <summary>
    /// The misbehaving pattern behind the classic deadlock: <paramref name="action"/> runs
    /// on this thread and is allowed to block synchronously (e.g. via .Result). If it does,
    /// this thread never reaches a pump loop, so any continuation posted back to this
    /// context — including the one <paramref name="action"/> is waiting on — can never run.
    /// </summary>
    public void RunBlocking(Action action)
    {
        SetSynchronizationContext(this);
        action();
    }
}
