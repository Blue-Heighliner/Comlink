namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IDisplayHandler"/> that renames every concept it can.</summary>
public sealed class TestDisplayHandler : IDisplayHandler
{
    /// <inheritdoc />
    public string? AppName => "MyApp";

    /// <inheritdoc />
    public string? DataFolderName => "MyApp";

    /// <inheritdoc />
    public string? Icon => "avares://Host/icon.png";

    /// <inheritdoc />
    public string? Version => "2.3.4";

    /// <inheritdoc />
    public bool IsKiosk => true;

    /// <inheritdoc />
    public string? HomeText => "Welcome";

    /// <inheritdoc />
    public string? AlertLabel => "ALARM";

    /// <inheritdoc />
    public string? TagLabel => "Category";

    /// <inheritdoc />
    public string? TagPluralLabel => "Categories";

    /// <inheritdoc />
    public string? PriorityLabel => "Importance";

    /// <inheritdoc />
    public string? SecurityLevelLabel => "Classification";

    /// <inheritdoc />
    public string? InboxLabel => "Received";
}
