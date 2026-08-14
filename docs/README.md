# AsyncPlayground

A console app of 20 self-contained C#/.NET async/await examples, ordered basic to
advanced. Every example is runnable, narrates what it's doing via timestamped,
thread-tagged console output, and is commented on the *why*, not just the *what*.

Target framework: `net10.0`. No external dependencies.

## Running it

```
dotnet run                # interactive menu
dotnet run -- list        # print the menu and exit
dotnet run -- all         # run every example in order
dotnet run -- 7           # run example #7 by its menu number
```

In the interactive menu, type a number to run that example, `a` to run all of them,
or `q` to quit. Ctrl+C cancels whichever example is currently running (via a shared
`CancellationToken`) instead of killing the process outright — several examples
specifically demonstrate what cancellation does and doesn't do, so this matters.

## How the project is organized

```
Examples/
  IAsyncExample.cs         the contract every example implements
  Support/                 shared helpers used across examples (see below)
  01_Basics/                Task vs Task<T>, why 'async void' is dangerous
  02_Composition/            sequential vs concurrent awaits, WhenAll vs WhenAny
  03_Exceptions/             exception propagation, the WhenAll aggregation gotcha
  04_Cancellation/           CancellationToken fundamentals, timeout patterns
  05_Synchronization/        SemaphoreSlim throttling, Channel producer/consumer
  06_Deadlocks/              the classic .Result deadlock, ConfigureAwait(false)
  07_Streaming/              IAsyncEnumerable<T> and await foreach
  08_Advanced/               ValueTask<T>, Parallel.ForEachAsync, TaskCompletionSource,
                             a hand-built custom awaitable, AsyncLocal<T>
  09_RealWorld/              retry with backoff, an async rate limiter, concurrent
                             downloads with progress + cancellation
Program.cs                  discovers examples via reflection and runs the menu
docs/
  README.md                 this file
  DEBUGGING.md               how to actually debug async code, tool by tool
  PITFALLS.md                a fast-reference checklist of the mistakes each example teaches
```

New examples don't need to be registered anywhere: `Program.cs` finds every class
implementing `IAsyncExample` via reflection and sorts them by `Category` then `Title`.
Drop a new class under `Examples/` and it appears in the menu automatically.

### Support helpers

- **`Support/Log.cs`** — prints `[HH:mm:ss.fff] [Thread N] message`. The thread number
  is the whole point: watching it change (or not) across an `await` is how you build
  real intuition for how the scheduler works, instead of taking it on faith.
- **`Support/Sim.cs`** — `Sim.FetchAsync(name, delayMs)` stands in for a real I/O call
  (HTTP, database, file) using `Task.Delay`, which is itself genuinely asynchronous and
  non-blocking, so it behaves like real I/O for every example without needing a network.
  `Sim.FlakyFetchAsync` adds a configurable failure rate for retry/resilience examples.
- **`Support/SingleThreadSyncContext.cs`** — a minimal stand-in for the single-threaded
  `SynchronizationContext` that WPF, WinForms, and classic ASP.NET install. Plain
  console apps have none by default, which is *why* the classic deadlock in
  `06_Deadlocks/ClassicDeadlock.cs` doesn't happen here on its own — this recreates the
  mechanism on purpose so it can be shown safely, without actually hanging the app.

## Suggested reading order

The menu is already ordered basic → advanced, so running `dotnet run -- all` once
top to bottom is a reasonable crash course. If you want the short version:

1. **01_Basics** — get the vocabulary right: `Task` vs `Task<T>`, what `await`
   actually suspends, why `async void` is a trap.
2. **02_Composition + 03_Exceptions** — the #1 real-world performance bug (awaiting
   sequentially instead of starting work concurrently) and the #1 real-world
   correctness bug (assuming `Task.WhenAll` surfaces every exception).
3. **04_Cancellation** — cancellation is cooperative, not preemptive; nothing stops
   unless code chooses to check.
4. **05_Synchronization + 06_Deadlocks** — bounding concurrency safely, and the
   specific mechanism behind the most infamous async bug in .NET history.
5. **07_Streaming, 08_Advanced, 09_RealWorld** — once the fundamentals are solid,
   these show the tools you reach for in production code: streaming results,
   `ValueTask<T>`, bridging callback APIs, and patterns like retry-with-backoff and
   rate limiting.

When something in an example still doesn't make sense from the console output alone,
switch to [DEBUGGING.md](DEBUGGING.md) — it walks through actually setting breakpoints
and watching this exact kind of code execute step by step. [PITFALLS.md](PITFALLS.md)
is the condensed "what to remember" version once you've been through the examples.
