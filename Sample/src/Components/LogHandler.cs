namespace BlueHeighliner.Comlink.Sample;

/// <summary>Fixes the widths of the fields of every log line so that lines align: the category is eight characters, the user as long as the longest user name of the scenarios and the event ID two digits.</summary>
public sealed class LogHandler : ILogHandler
{
    /// <inheritdoc />
    public int? CategoryWidth => 8;

    /// <inheritdoc />
    public int? UserWidth => 8;

    /// <inheritdoc />
    public int? IdWidth => 2;
}
