namespace BlueHeighliner.Comlink.Sample;

/// <summary>Gives the Sample its own window icon and home text, leaving the names of the app's concepts as the engine has them.</summary>
public sealed class DisplayHandler : IDisplayHandler
{
    /// <inheritdoc />
    public string? Icon => "avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png";

    /// <inheritdoc />
    public string? HomeText => "Select a folder and entry to get started, or create a new draft or note.";
}
