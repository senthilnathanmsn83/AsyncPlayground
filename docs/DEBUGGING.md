# Debugging async C# — a practical guide

Async code is harder to debug than sync code for one structural reason: a single
logical operation is spread across multiple *stack frames in time*, not just in space.
A breakpoint inside an `async` method only shows you the state machine's current
resumption, not "the call stack that led here" in the traditional sense. This guide is
the set of techniques that actually work, organized by the kind of problem you're
chasing. Where useful, it points at the specific example in this repo that demonstrates
the underlying mechanism.

## 1. Breakpoints across `await`

A breakpoint on a line after an `await` will still hit — the debugger handles this
transparently — but two things behave differently from sync code:

- **The call stack past the `await` is synthetic.** Visual Studio and Rider reconstruct
  an "async call stack" using `StateMachineAttribute` metadata so it *looks* continuous,
  but frames below the resumption point are stitched together, not real return
  addresses. This is normally fine to read, but don't expect `Debug.Print` of a raw
  stack trace to match what the debugger shows.
- **"Step Over" (F10) on an `await` will step over the entire suspend/resume**, landing
  you on the next line after the operation completes — potentially seconds later and
  possibly on a different thread. If you want to see what happens *during* the await
  (e.g. verifying a continuation actually runs), set a breakpoint on the line after it
  and let the program run (F5), rather than stepping through.

Try this on `Examples/01_Basics/TaskVsTaskOfT.cs` — put a breakpoint after
`await resultTask;` and one on the line right before it. Run it, and check the **Call
Stack** window's Thread ID and the debugger's thread indicator at each breakpoint: they
frequently differ, because nothing guarantees the continuation resumes on the same
thread pool thread it suspended on.

## 2. Watching thread hops with Debug > Windows > Threads

The **Threads** window (Visual Studio: Debug → Windows → Threads; Rider: the Threads
tab in the debug panel) lists every live thread and flags the current one. Pause
execution mid-await-chain in `Examples/08_Advanced/AsyncLocalExample.cs` and step
through — you'll see the "current thread" flag jump between thread-pool worker threads
across each `await`, while `AsyncLocal<string?>` still reports the correct value on
whichever thread you land on. That's the concrete difference between thread-local state
(breaks across hops) and async-local state (doesn't).

This repo's `Support/Log.cs` prints `Environment.CurrentManagedThreadId` on every line
specifically so you can correlate console output with what the Threads window shows
without needing to pause at all — run any example and just read the `[Thread N]` tags.

## 3. Parallel Stacks and Tasks windows (Visual Studio)

For anything running multiple concurrent operations — `Examples/05_Synchronization/*`,
`Examples/08_Advanced/ParallelForEachAsyncExample.cs`, `Examples/09_RealWorld/*` — the
single-threaded Call Stack window stops being useful. Two better tools:

- **Debug → Windows → Parallel Stacks** shows every thread's call stack simultaneously
  in a graph, with threads sharing a common stack frame merged into one node. This is
  the fastest way to see "which threads are all stuck at the same `await`."
- **Debug → Windows → Tasks** lists every `Task` the runtime knows about — its status
  (Running, WaitingForActivation, RanToCompletion...), which thread it's on, and where
  it was scheduled from. A task stuck in `WaitingForActivation` for a long time next to
  one stuck in `Blocked` is exactly the shape of the deadlock in
  `Examples/06_Deadlocks/ClassicDeadlock.cs` — pause it mid-hang (before the 2-second
  detection timeout fires) and open this window to see both halves of the deadlock at
  once.

VS Code / `vsdbg` and Rider don't have exact equivalents of Parallel Stacks, but
Rider's **Threads & Variables** panel with "Show Threads" toggled, combined with
`!dumpasync` in a memory dump (see §6), gets you similar information.

## 4. Diagnosing sync-over-async deadlocks

The signature symptom: the app just stops responding, CPU usage is near zero, and
there's no exception — because nothing failed, two things are just waiting on each
other forever. `Examples/06_Deadlocks/ClassicDeadlock.cs` reproduces this exact
condition on purpose (safely bounded with a timeout so the demo app doesn't actually
hang) and its comments explain the mechanism in detail. In production:

1. **Look for `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`** anywhere in the
   stack near the hang. This is the single most common root cause. Grep the codebase
   for these before doing anything else.
2. If you can attach a debugger while it's hung, **open the Threads window** and look
   for a thread blocked inside `.Result`/`.Wait()`. Then check the **Tasks window** for
   a task that's `WaitingForActivation` and whose continuation needs to run on the
   context of the blocked thread (a UI thread, or a captured `SynchronizationContext`).
   That pairing *is* the deadlock.
3. If you can't attach live, capture a memory dump (`dotnet-dump collect`, or
   Task Manager → "Create dump file" on Windows) and analyze it offline — see §6.
4. **Fix**, in order of preference: don't block at all (make the caller `async` too —
   "async all the way up/down"); if you truly cannot, add `.ConfigureAwait(false)` to
   every `await` in the awaited call chain, as shown in
   `Examples/06_Deadlocks/ConfigureAwaitExplained.cs`, so continuations stop trying to
   resume on the blocked thread's context.

## 5. Thread pool starvation (the *other* "everything is slow")

A different failure mode that looks similar from the outside (things slow down or
stall) but has a different cause: too much work is queued onto the thread pool at once
— often from blocking calls (`Task.Run(() => syncMethod())`, `.Result` on code that
*doesn't* deadlock but does tie up a worker thread) — and the pool's slow ramp-up
algorithm can't inject new threads fast enough to keep up.

Signs: CPU usage that doesn't match throughput, and delays that scale with concurrent
load rather than any individual operation's latency. Diagnose with:

```
dotnet-counters monitor -p <pid> --counters System.Runtime
```

Watch `ThreadPool Queue Length` and `ThreadPool Thread Count` — a queue length that
keeps climbing while the thread count grows only slowly is thread pool starvation.
Run `Examples/05_Synchronization/SemaphoreThrottling.cs` and
`Examples/08_Advanced/ParallelForEachAsyncExample.cs` side by side with
`dotnet-counters` attached to compare a properly-async bounded-concurrency pattern
(no thread pool pressure — the queued *tasks* wait, not threads) against what happens
if you rewrite one to call a blocking method inside `Task.Run` instead.

Install the tool once, if you don't have it: `dotnet tool install -g dotnet-counters`.

## 6. Command-line diagnostics: `dotnet-trace`, `dotnet-dump`, `dotnet-counters`

These work anywhere, including in containers/servers with no attached debugger, and
are worth knowing even if you mostly use an IDE debugger locally.

```
dotnet tool install -g dotnet-trace
dotnet tool install -g dotnet-dump
dotnet tool install -g dotnet-counters
```

- **`dotnet-counters monitor -p <pid>`** — a live dashboard of GC, thread pool,
  exception, and JIT counters. Start here for "something is slow" before reaching for
  a full trace; it's near-zero overhead.
- **`dotnet-trace collect -p <pid>`** then open the resulting `.nettrace` in
  **PerfView** or Visual Studio's profiler — gives a CPU/async flame view showing where
  time is actually spent, including inside `await` continuations. This is the right
  tool when `dotnet-counters` shows a symptom but not a cause.
- **`dotnet-dump collect -p <pid>`** captures a full memory snapshot for offline
  analysis (`dotnet-dump analyze <path>`), then `dumpasync` (via the SOS extension,
  `dotnet-sos install` once) lists every live async state machine and where it's
  stuck — the offline equivalent of the Tasks window in §3, and the standard way to
  diagnose a hung process in production where you can't attach a live debugger.

## 7. Unhandled exceptions you won't see coming

- **`async void` methods.** `Examples/01_Basics/AsyncVoidPitfall.cs` demonstrates
  exactly where an exception thrown from `async void` actually goes — it's posted
  through the current `SynchronizationContext`, not thrown back to the caller. In a
  real console app (no context installed) it becomes a genuinely unhandled exception
  that crashes the process via `AppDomain.UnhandledException`. Set a breakpoint in
  Visual Studio on that event (or enable **Debug → Windows → Exception Settings →
  Common Language Runtime Exceptions → break on throw** for the exception type) to
  catch it at the actual throw site instead of wherever the crash surfaces.
- **Unobserved task exceptions.** If a `Task` fails and nothing ever awaits it or reads
  `.Exception`, the exception is silently dropped by default (since .NET 4.5,
  unobserved task exceptions no longer crash the process). Subscribe to
  `TaskScheduler.UnobservedTaskException` during development to catch these — they're
  otherwise invisible:
  ```csharp
  TaskScheduler.UnobservedTaskException += (_, e) =>
  {
      Console.WriteLine($"Unobserved: {e.Exception}");
      e.SetObserved();
  };
  ```
- **`Task.WhenAll` hiding all-but-one exception.** See §"Exceptions" below and
  `Examples/03_Exceptions/ExceptionHandling.cs` — `await`ing a failed `WhenAll` only
  rethrows the first exception; the rest are still there in `task.Exception`, and
  logging only the caught one will hide real failures.

## 8. Tracing a logical operation across threads

Manually correlating log lines from a multi-threaded async operation is what
`Support/Log.cs`'s thread tagging is *for* — but it doesn't scale past a demo. In real
code, use `System.Diagnostics.Activity` (the .NET-native distributed tracing type,
what `ILogger` scopes and OpenTelemetry both build on) instead of hand-rolled
correlation IDs. It's built on the same `AsyncLocal<T>` mechanism shown in
`Examples/08_Advanced/AsyncLocalExample.cs`, so it automatically survives thread hops
the way a `[ThreadStatic]` field never would:

```csharp
using var activity = new Activity("ProcessOrder").Start();
activity.SetTag("order.id", orderId);
// activity.Current flows through every await below, across every thread hop
```

## 9. Quick checklist when an async bug won't reproduce

Async bugs are frequently timing-dependent, which makes them vanish under a debugger
(pausing changes the interleaving) or reproduce only under real load. Before assuming
it's unfixable:

- Add artificial `await Task.Delay(...)` jitter around suspected race points locally to
  widen the timing window instead of narrowing it — the opposite of what a debugger
  does, and often enough to reproduce a race reliably.
- Check for missing `ConfigureAwait(false)` / captured-context issues only in code
  that's actually reached from a context-having caller (UI, ASP.NET request) — this
  class of bug frequently doesn't reproduce in a unit test or console harness at all,
  which is exactly why `Examples/06_Deadlocks/*` has to build a fake context to show it.
  See `Support/SingleThreadSyncContext.cs`.
  This is also the reason `Examples/06_Deadlocks/ClassicDeadlock.cs` runs its blocking
  call on a background `Thread` with a bounded `Join` timeout rather than the main
  thread — so a genuine reproduction doesn't just hang the whole demo app.
- If concurrency-order is the suspected culprit, prefer `Task.WhenAll(...)` /
  `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` turned down to 1 as a bisection
  tool — if the bug disappears at concurrency 1, it's a race, not a logic bug.
