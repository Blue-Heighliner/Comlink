namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>AvaloniaEdit <see cref="TextDocument"/>-backed implementation of <see cref="IBodyDocument"/> for use in the draft editor UI.</summary>
internal sealed class TextDocumentBodyDocument : IBodyDocument
{
    /// <summary>Gets the underlying AvaloniaEdit <see cref="TextDocument"/>.</summary>
    public TextDocument Document { get; } = new TextDocument();

    /// <inheritdoc />
    public string Text { get => Document.Text; set => Document.Text = value; }

    /// <inheritdoc />
    public int TextLength => Document.TextLength;

    /// <inheritdoc />
    public void Insert(int offset, string text) => Document.Insert(offset, text);
}
