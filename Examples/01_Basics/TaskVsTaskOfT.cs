using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Basics;

/// <summary>
/// The absolute fundamentals: what "async Task" vs "async Task&lt;T&gt;" mean, and what
/// actually happens at an `await`. Start here if async/await is new to you.
/// </summary>
sealed class TaskVsTaskOfT : IAsyncExample
{
    public string Category => "01. Basics async";
    public string Title => "Task vs Task<T>, and what await really does";
    public string Summary => "Shows the difference between a void-like async method and one returning a value, and proves control returns to the caller during an await.";

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Section("Task (no return value) vs Task<T> (returns a value)");

        // Task represents "an operation that completes", roughly like void but awaitable.
        await DoWorkAsync();

        // Task<T> represents "an operation that completes and produces a T".
        int total = await ComputeTotalAsync();
        Log.Write($"ComputeTotalAsync returned {total}");

        Log.Section("Proving await yields control instead of blocking");

        Log.Write("about to await Sim.FetchAsync — thread will NOT block here");
        var resultTask = Sim.FetchAsync("inventory-service", 300, ct);

        // The line below runs immediately, before the fetch above completes, because
        // starting the async call and awaiting it are two separate steps: the call
        // returns a Task right away, and only `await` suspends *this* method.
        Log.Write("this line runs immediately after calling FetchAsync, before it finishes");

        string result = await resultTask; // suspension point: method resumes when the Task completes
        Log.Write($"resumed after await with: {result}");
    }

    private static async Task DoWorkAsync()
    {
        Log.Write("DoWorkAsync: doing some awaited work...");
        await Task.Delay(100);
        Log.Write("DoWorkAsync: finished (no value to return)");
    }

    private static async Task<int> ComputeTotalAsync()
    {
        Log.Write("ComputeTotalAsync: computing...");
        await Task.Delay(100);
        return 42; // becomes the result the caller gets from `await ComputeTotalAsync()`
    }
}
