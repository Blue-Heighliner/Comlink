namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IMessageTagConfiguration"/> that honors <c>config.json</c>'s <c>MessageTagsEnabled</c>
/// exactly like the Engine default, falling back to the <c>TAGS_ENABLED</c> environment variable
/// (<c>"0"</c>/<c>"false"</c> to disable), then <see langword="true"/>. Also renames the tag input's label
/// to <c>"Category"</c>, demonstrating <see cref="IMessageTagConfiguration.TagLabel"/>.
/// </summary>
public sealed class SampleMessageTagConfiguration : IMessageTagConfiguration
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SampleMessageTagConfiguration"/> with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the optional tags-enabled setting.</param>
    public SampleMessageTagConfiguration(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public bool TagsEnabled => _config.MessageTagsEnabled ?? ReadEnvFlag() ?? true;
    /// <inheritdoc />
    public string TagLabel => string.IsNullOrEmpty(_config.MessageTagLabel) ? "Category" : _config.MessageTagLabel;

    private static bool? ReadEnvFlag()
    {
        string? value = Environment.GetEnvironmentVariable("TAGS_ENABLED");
        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        return null;
    }
}
