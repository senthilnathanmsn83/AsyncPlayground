# Async pitfalls — quick reference

A fast-scan checklist of every mistake this repo's examples demonstrate. Each row links
to the example that shows the mistake happening (and the fix) in runnable code.

| # | Pitfall | Why it happens | Fix | Example |
|---|---------|-----------------|-----|---------|
| 1 | `async void` swallows exceptions | Exceptions post through `SynchronizationContext`, not back to the caller's try/catch | Use `async Task` everywhere except event handlers | `01_Basics/AsyncVoidPitfall.cs` |
| 2 | Awaiting independent calls sequentially | Each `await` blocks starting the next call until it finishes | Start all the tasks first, `await Task.WhenAll(...)` after | `02_Composition/SequentialVsConcurrent.cs` |
| 3 | Using `WhenAny` where every result is needed | `WhenAny` resolves on the first completion; the rest are abandoned unless awaited/cancelled | Use `WhenAll` when you need every result; explicitly cancel losers in a race | `02_Composition/WhenAllWhenAny.cs` |
| 4 | Only one exception surfaces from a failed `WhenAll` | `await` on the combined task rethrows just the first inner exception | Inspect `task.Exception.InnerExceptions` for the full set | `03_Exceptions/ExceptionHandling.cs` |
| 5 | Cancellation "not working" | `CancellationToken` is cooperative — code must check it; nothing is preempted | Pass the token all the way down, call `ThrowIfCancellationRequested()` in loops | `04_Cancellation/CancellationBasics.cs` |
| 6 | Timeout doesn't stop the underlying work | A timeout on the *wait* (`WaitAsync`, a separate `CancelAfter`) doesn't cancel work that wasn't given that same token | Pass the (linked) timeout token into the actual operation, not just the wrapper | `04_Cancellation/TimeoutPatterns.cs` |
| 7 | Unbounded concurrency overwhelms a downstream service | Firing N tasks at once with no cap | `SemaphoreSlim.WaitAsync`/`Release`, or `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` | `05_Synchronization/SemaphoreThrottling.cs`, `08_Advanced/ParallelForEachAsyncExample.cs` |
| 8 | Producer outruns consumers, memory grows unbounded | An unbounded queue between producer and consumer has no backpressure | `Channel.CreateBounded<T>` with `FullMode = Wait` | `05_Synchronization/ProducerConsumerChannel.cs` |
| 9 | The classic `.Result`/`.Wait()` deadlock | A blocking call on a thread that owns a `SynchronizationContext`, waiting on a continuation that needs that same thread | Don't block — await instead; or `.ConfigureAwait(false)` on the awaited chain as a workaround | `06_Deadlocks/ClassicDeadlock.cs` |
| 10 | Confusing "which thread does this resume on" | `await` without `ConfigureAwait(false)` tries to resume on the captured context; with it, resumes on any thread-pool thread | Use `ConfigureAwait(false)` in library code with no UI/request context to get back to | `06_Deadlocks/ConfigureAwaitExplained.cs` |
| 11 | `[ThreadStatic]`/static fields leak state between concurrent logical operations | Async code hops threads across every `await`; thread-affinity storage doesn't follow the *logical* operation | `AsyncLocal<T>` (or `Activity`), which flows with the async call, not the thread | `08_Advanced/AsyncLocalExample.cs` |
| 12 | Awaiting a `ValueTask<T>` twice, or concurrently | Some `ValueTask<T>` instances are backed by a reusable/pooled `IValueTaskSource<T>` with a single-use token | Await each `ValueTask<T>` exactly once; use `Task<T>` when in doubt | `08_Advanced/ValueTaskExample.cs` |
| 13 | Retrying without backoff or jitter | Fixed-interval retries from many clients synchronize and hammer the server in bursts | Exponential backoff with randomized jitter | `09_RealWorld/RetryWithBackoff.cs` |

## The five rules that cover most of the above

1. **Never `async void`, except an event handler.** If you can't `await` it, that's the
   warning sign, not a convenience.
2. **Start independent work before awaiting any of it.** Sequential `await`s are a
   choice, not a requirement — make it deliberately.
3. **Cancellation and timeouts only do what code explicitly checks for.** Passing a
   token around and never reading it does nothing.
4. **Never block on async code with `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`**
   from a caller you don't fully control. If you must, `.ConfigureAwait(false)` the
   entire awaited chain underneath, but prefer making the caller `async` instead.
5. **`Task.WhenAll` and `ValueTask<T>` both have a "read the docs before you assume"
   gotcha** — the former on exception aggregation, the latter on reuse. Both are shown
   failing in this repo specifically because the failure mode is non-obvious from the
   method signature alone.
