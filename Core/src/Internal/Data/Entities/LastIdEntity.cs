namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document holding the identifier the engine last generated, so an <see cref="IMessageHandler{TFrame}.NextId"/> can continue from it after a restart.</summary>
internal sealed class LastIdEntity
{
    /// <summary>Gets or sets the document key; there is only ever one document.</summary>
    public int Id { get; set; } = 1;
    /// <summary>Gets or sets the last identifier generated.</summary>
    public string Value { get; set; } = string.Empty;
}
