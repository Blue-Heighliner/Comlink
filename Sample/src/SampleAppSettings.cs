namespace BlueHeighliner.Comlink.Sample;

/// <summary>Sample <see cref="IAppSettings"/> providing a product-appropriate home screen welcome text; every other member uses the Engine default.</summary>
public sealed class SampleAppSettings : DefaultAppSettings
{
    /// <inheritdoc />
    public override string GetHomeText() =>
        "Select a folder and entry to get started, or create a new draft or note.";
}
