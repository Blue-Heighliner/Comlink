namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="ILogHandler"/> that fixes the widths of the level, category, user and event ID fields.</summary>
public sealed class TestLogHandler : ILogHandler
{
    /// <inheritdoc />
    public int? CategoryWidth => 8;

    /// <inheritdoc />
    public int? UserWidth => 7;

    /// <inheritdoc />
    public int? IdWidth => 3;
}
