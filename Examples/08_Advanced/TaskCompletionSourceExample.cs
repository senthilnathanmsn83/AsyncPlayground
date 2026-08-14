using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Advanced;

/// <summary>
/// TaskCompletionSource&lt;T&gt; is how you bridge a callback-based or event-based API
/// into something awaitable — it's the tool underneath most "Async" wrapper methods for
/// legacy APIs. This wraps a fake event-based subscription service in a Task.
/// </summary>
sealed class TaskCompletionSourceExample : IAsyncExample
{
    public string Category => "08. Advanced";
    public string Title => "Bridging callbacks to Task with TaskCompletionSource<T>";
    public string Summary => "Wraps an event-based (callback-style) API in an awaitable Task, the standard pattern for adapting legacy or third-party async patterns.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Wrapping a callback-based subscription in an awaitable Task");
        var service = new LegacyEventBasedService();

        string result = await SubscribeOnceAsync(service, ct);
        Log.Write($"awaited result from a callback API: {result}", ConsoleColor.Green);

        Log.Section("Same pattern, but the callback reports failure");
        try
        {
            await SubscribeOnceAsync(service, ct, forceFailure: true);
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"the callback's error path became a real exception: {ex.Message}", ConsoleColor.Yellow);
        }
    }

    private static Task<string> SubscribeOnceAsync(LegacyEventBasedService service, CancellationToken ct, bool forceFailure = false)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation so the awaited Task doesn't hang forever if the caller
        // cancels — without this, TaskCompletionSource never completes on its own.
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        void OnCompleted(object? sender, string data) => tcs.TrySetResult(data);
        void OnFailed(object? sender, Exception error) => tcs.TrySetException(error);

        service.Completed += OnCompleted;
        service.Failed += OnFailed;

        service.BeginOperation(forceFailure);

        return tcs.Task.ContinueWith(t =>
        {
            service.Completed -= OnCompleted;
            service.Failed -= OnFailed;
            return t.GetAwaiter().GetResult();
        }, TaskScheduler.Default);
    }

    /// <summary>Stand-in for an old-style API that only knows how to report completion via events.</summary>
    private sealed class LegacyEventBasedService
    {
        public event EventHandler<string>? Completed;
        public event EventHandler<Exception>? Failed;

        public void BeginOperation(bool forceFailure)
        {
            Log.Write("legacy service: operation started (fire-and-forget from its perspective)");
            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                if (forceFailure)
                    Failed?.Invoke(this, new InvalidOperationException("legacy service reported an error"));
                else
                    Completed?.Invoke(this, "legacy-payload");
            });
        }
    }
}
