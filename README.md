# AsyncPlayground

A console app of 20 runnable C#/.NET async/await examples, basic to advanced, with a
debugging guide and a pitfalls cheat sheet.

```
dotnet run                # interactive menu
dotnet run -- all         # run every example in order
```

See **[docs/README.md](docs/README.md)** for the full guide, **[docs/DEBUGGING.md](docs/DEBUGGING.md)**
for how to actually debug async code (breakpoints, deadlock diagnosis, thread pool
starvation, dotnet-trace/dotnet-dump), and **[docs/PITFALLS.md](docs/PITFALLS.md)** for
a condensed reference table of every mistake the examples demonstrate.
