namespace AsyncPlayground.Examples.Support;

/// <summary>
/// Timestamped, thread-tagged console output. Seeing the thread id change (or not)
/// across await points is the single most useful signal for building intuition about
/// how async/await actually schedules work — this exists to make that visible everywhere.
/// </summary>
static class Log
{
    private static readonly object ConsoleLock = new();

    public static void Write(string message, ConsoleColor? color = null)
    {
        lock (ConsoleLock)
        {
            if (color is not null) Console.ForegroundColor = color.Value;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId,2}] {message}");
            if (color is not null) Console.ResetColor();
        }
    }

    public static void Section(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"── {title} ──");
        Console.ResetColor();
    }
}
