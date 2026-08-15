namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>Abstraction over body text storage for a draft message, independent of any specific UI framework.</summary>
public interface IBodyDocument
{
    /// <summary>Gets or sets the full text content of the document.</summary>
    string Text { get; set; }
    /// <summary>Gets the number of characters in the document.</summary>
    int TextLength { get; }
    /// <summary>Inserts text at the specified character offset.</summary>
    /// <param name="offset">The zero-based character position at which to insert.</param>
    /// <param name="text">The text to insert.</param>
    void Insert(int offset, string text);
}

/// <summary>Simple string-backed implementation of <see cref="IBodyDocument"/> for non-Avalonia contexts such as unit tests.</summary>
public sealed class StringBodyDocument : IBodyDocument
{
    private string text = string.Empty;

    /// <inheritdoc />
    public string Text { get => text; set => text = value; }

    /// <inheritdoc />
    public int TextLength => text.Length;

    /// <inheritdoc />
    public void Insert(int offset, string text) => text = text.Insert(offset, text);
}

/// <summary>Factory that creates <see cref="IBodyDocument"/> instances for use by draft ViewModels.</summary>
public interface IBodyDocumentFactory
{
    /// <summary>Creates a new <see cref="IBodyDocument"/> instance.</summary>
    /// <returns>A new, empty body document.</returns>
    IBodyDocument Create();
}

/// <summary>Default <see cref="IBodyDocumentFactory"/> that creates <see cref="StringBodyDocument"/> instances.</summary>
public sealed class BodyDocumentFactory : IBodyDocumentFactory
{
    /// <inheritdoc />
    public IBodyDocument Create() => new StringBodyDocument();
}
