namespace AsyncPlayground.Examples;

/// <summary>
/// Contract every example implements. Keeping this minimal means Program.cs can discover
/// and run examples uniformly, and each example file stays self-contained and readable
/// top to bottom, which matters more here than in production code.
/// </summary>
interface IAsyncExample
{
    /// <summary>Menu grouping, e.g. "01. Basics".</summary>
    string Category { get; }

    /// <summary>Short name shown in the menu.</summary>
    string Title { get; }

    /// <summary>One or two sentences: what this teaches and why it matters.</summary>
    string Summary { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
