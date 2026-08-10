namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>Factory that creates <see cref="TextDocumentBodyDocument"/> instances for the Avalonia draft editor.</summary>
internal sealed class TextDocumentBodyDocumentFactory : IBodyDocumentFactory
{
    /// <inheritdoc />
    public IBodyDocument Create() => new TextDocumentBodyDocument();
}
