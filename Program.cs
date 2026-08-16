using System.Diagnostics;
using AsyncPlayground.Examples;

namespace AsyncPlayground;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        List<IAsyncExample> examples = DiscoverExamples();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // don't let the runtime kill us immediately — cancel the running example instead
            Console.WriteLine();
            Console.WriteLine("Ctrl+C received — cancelling the running example...");
            cts.Cancel();
        };

        if (args.Length > 0)
            return await RunNonInteractiveAsync(args, examples, cts.Token);

        await RunInteractiveMenuAsync(examples, cts.Token);
        return 0;
    }

    /// <summary>
    /// Reflection-based discovery means adding a new example is just "drop a class
    /// implementing IAsyncExample somewhere under Examples/" — nothing to register.
    /// </summary>
    private static List<IAsyncExample> DiscoverExamples() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(IAsyncExample).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(t => (IAsyncExample)Activator.CreateInstance(t)!)
            .OrderBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.Title, StringComparer.Ordinal)
            .ToList();

    private static async Task<int> RunNonInteractiveAsync(string[] args, List<IAsyncExample> examples, CancellationToken ct)
    {
        switch (args[0].TrimStart('-').ToLowerInvariant())
        {
            case "list":
                PrintMenu(examples);
                return 0;

            case "all":
                foreach (var example in examples)
                    await RunOneAsync(example, ct);
                return 0;

            default:
                if (int.TryParse(args[0], out int index) && index >= 1 && index <= examples.Count)
                {
                    await RunOneAsync(examples[index - 1], ct);
                    return 0;
                }

                Console.WriteLine($"Unknown argument '{args[0]}'. Use a 1-based example number, 'all', or 'list'.");
                return 1;
        }
    }

    private static async Task RunInteractiveMenuAsync(List<IAsyncExample> examples, CancellationToken ct)
    {
        while (true)
        {
            PrintMenu(examples);
            Console.WriteLine();
            Console.Write("Pick a number (or 'a' = run all, 'q' = quit): ");
            string? input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) || input.Equals("q", StringComparison.OrdinalIgnoreCase))
                return;

            if (input.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var example in examples)
                    await RunOneAsync(example, ct);
                continue;
            }

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= examples.Count)
            {
                await RunOneAsync(examples[choice - 1], ct);
            }
            else
            {
                Console.WriteLine("Not a valid choice.");
            }
        }
    }

    private static void PrintMenu(List<IAsyncExample> examples)
    {
        Console.WriteLine();
        Console.WriteLine("=================================================================");
        Console.WriteLine(" AsyncPlayground — async/await examples, basic to advanced");
        Console.WriteLine(" Docs: docs/README.md   Debugging guide: docs/DEBUGGING.md");
        Console.WriteLine("=================================================================");

        string? lastCategory = null;
        for (int i = 0; i < examples.Count; i++)
        {
            IAsyncExample example = examples[i];
            if (example.Category != lastCategory)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(example.Category);
                Console.ResetColor();
                lastCategory = example.Category;
            }

            Console.WriteLine($"  {i + 1,2}. {example.Title}");
            Console.WriteLine($"      {example.Summary}");
        }
    }

    private static async Task RunOneAsync(IAsyncExample example, CancellationToken ct)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($">>> Running: {example.Title}");
        Console.ResetColor();

        var sw = Stopwatch.StartNew();
        try
        {
            await example.RunAsync(ct);
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"<<< Completed in {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("<<< Cancelled");
        }
        catch (Exception ex)
        {
            // Deliberately catching everything here: this is the top-level runner, and an
            // uncaught exception from one example shouldn't take down the whole menu.
            // Compare to Examples/01_Basics/AsyncVoidPitfall.cs, where NOTHING can catch
            // an exception from an async void method the way this catches async Task ones.
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"<<< Unhandled exception: {ex}");
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
