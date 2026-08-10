namespace BlueHeighliner.Comlink.Sample;

/// <summary>Provides the home screen welcome text for the Sample application.</summary>
public sealed class SampleHomeContentProvider : IHomeContentProvider
{
    /// <inheritdoc />
    public string GetHomeText() =>
        "Select a folder and entry to get started, or create a new draft or note.";
}
