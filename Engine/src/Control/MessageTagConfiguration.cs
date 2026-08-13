namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Controls whether message tags are used anywhere in the UI.</summary>
public interface IMessageTagConfiguration
{
    /// <summary>
    /// When <see langword="true"/>, the draft editor shows a tag input and the entry listing shows each
    /// message's tag next to its priority. When <see langword="false"/>, tags are hidden everywhere in the
    /// UI — the underlying <see cref="IMessageFormat.GetTag"/>/<see cref="IMessageFormat.SetTag"/> values on
    /// existing messages are left untouched, just not surfaced.
    /// </summary>
    bool TagsEnabled { get; }
    /// <summary>
    /// The label used for the tag input's watermark in the draft editor. Lets a host call the concept
    /// something other than "Tag" (e.g. "Category", "Type") without changing engine behavior.
    /// </summary>
    string TagLabel { get; }
}

/// <summary>Implements <see cref="IMessageTagConfiguration"/> driven by <see cref="EngineConfig"/>, enabled by default with the label "Tag".</summary>
internal sealed class MessageTagConfiguration : IMessageTagConfiguration
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="MessageTagConfiguration"/> reading from the given engine configuration.</summary>
    public MessageTagConfiguration(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public bool TagsEnabled => _config.MessageTagsEnabled ?? true;
    /// <inheritdoc />
    public string TagLabel => string.IsNullOrEmpty(_config.MessageTagLabel) ? "Tag" : _config.MessageTagLabel;
}
