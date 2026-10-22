namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="ILogger"/> the engine offers, which records every entry written to it so a test can see what a component logged. Like the engine's, it refuses an entry with no identifier.</summary>
public sealed class FakeLogger : ILogger
{
    /// <summary>Gets every entry written so far, in the order written.</summary>
    public List<FakeLogEntry> Entries { get; } = [];

    /// <inheritdoc />
    public void Log(string category, string id, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Entries.Add(new FakeLogEntry(category, id, message, null));
    }

    /// <inheritdoc />
    public void Log(string category, string id, Exception exception, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Entries.Add(new FakeLogEntry(category, id, message, exception));
    }
}
