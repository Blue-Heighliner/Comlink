namespace BlueHeighliner.Comlink;

/// <summary>A custom import format added via <see cref="IEngineBuilder.ImportFormat{TFormat}"/>.</summary>
internal sealed record ImportFormatDefinition
{
    /// <summary>Display name shown for this format in the client's import screen, and the source of <see cref="FileExtension"/>.</summary>
    public required string Name { get; init; }
    /// <summary>Reads one file's stream, adding what it finds through the handed context.</summary>
    public required Func<Stream, IImportFormatContext, CancellationToken, Task> Read { get; init; }
    /// <summary>Whether this format's staged sends are all sent at once, or one at a time.</summary>
    public StagedSendMode StagedSendMode { get; init; } = StagedSendMode.Sequential;
    /// <summary>While <see cref="StagedSendMode"/> is <see cref="StagedSendMode.Sequential"/>, an optional pause between each send.</summary>
    public TimeSpan? StagedSendDelay { get; init; }
    /// <summary>File extension (without the leading dot) this format's files are found by on a drive, derived from <see cref="Name"/> the same way an <see cref="ExportFormatDefinition"/> entry's is: lowercased and stripped to letters and digits, falling back to <c>dat</c> if that leaves nothing.</summary>
    public string FileExtension => new string([.. Name.Where(char.IsLetterOrDigit)]) is { Length: > 0 } letters
        ? letters.ToLowerInvariant()
        : "dat";
}
