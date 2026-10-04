namespace BlueHeighliner.Comlink.Sample;

/// <summary>Gives the Sample its own home text and its own names for the concepts of the app, everywhere the user interface mentions them.</summary>
public sealed class DisplayHandler : IDisplayHandler
{
    /// <inheritdoc />
    public string? Icon => "avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png";

    /// <inheritdoc />
    public string? HomeText => "Select a folder and entry to get started, or create a new draft or note.";

    /// <inheritdoc />
    public string? TagLabel => "Category";

    /// <inheritdoc />
    public string? TagPluralLabel => "Categories";

    /// <inheritdoc />
    public string? PriorityLabel => "Importance";

    /// <inheritdoc />
    public string? PriorityPluralLabel => "Importance levels";

    /// <inheritdoc />
    public string? SecurityLevelLabel => "Confidentiality";

    /// <inheritdoc />
    public string? SecurityLevelPluralLabel => "Confidentiality levels";

    /// <inheritdoc />
    public string? InboxLabel => "Received";

    /// <inheritdoc />
    public string? OutboxLabel => "Sent";

    /// <inheritdoc />
    public string? ActivityLabel => "History";
}
