namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>AvaloniaEdit element generator that replaces fill-in sentinel markers with inline button controls.</summary>
[ExcludeFromCodeCoverage]
public sealed class FillInElementGenerator : VisualLineElementGenerator
{
    /// <summary>The Unicode PUA character used to mark the start of a fill-in marker in the document.</summary>
    public const char Sentinel = '';
    /// <summary>The number of hex characters in a fill-in ID.</summary>
    public const int IdLength = 8;
    /// <summary>The total length of a fill-in marker (sentinel character plus ID).</summary>
    public const int MarkerLength = IdLength + 1;

    private readonly IReadOnlyDictionary<string, IFillInViewModel> _fillIns;

    /// <summary>Initializes the generator with the fill-in ViewModel map for the active draft.</summary>
    public FillInElementGenerator(IReadOnlyDictionary<string, IFillInViewModel> fillIns)
    {
        _fillIns = fillIns;
    }

    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset)
    {
        DocumentLine line = CurrentContext.VisualLine.FirstDocumentLine;
        int lineEnd = line.Offset + line.Length;
        if (startOffset >= lineEnd) return -1;

        string text = CurrentContext.Document.GetText(startOffset, lineEnd - startOffset);
        int idx = text.IndexOf(Sentinel);
        return idx < 0 ? -1 : startOffset + idx;
    }

    /// <inheritdoc />
    public override VisualLineElement? ConstructElement(int offset)
    {
        TextDocument doc = CurrentContext.Document;
        if (doc.GetCharAt(offset) != Sentinel) return null;
        if (offset + MarkerLength > doc.TextLength) return null;

        string id = doc.GetText(offset + 1, IdLength);
        if (!_fillIns.TryGetValue(id, out IFillInViewModel? fillIn)) return null;

        FillInInlineControl ctrl = new()
        {
            CharWidth = MeasureCharAdvanceWidth(),
            DataContext = fillIn,
        };
        return new InlineObjectElement(MarkerLength, ctrl);
    }

    private double MeasureCharAdvanceWidth()
    {
        TextRunProperties props = CurrentContext.GlobalTextRunProperties;
        // TryGetGlyphTypeface works correctly when it returns true.
        if (FontManager.Current.TryGetGlyphTypeface(props.Typeface, out IGlyphTypeface? gt))
        {
            ushort glyph = gt.GetGlyph((uint)'x');
            return (double)gt.GetGlyphAdvance(glyph)
                   / gt.Metrics.DesignEmHeight
                   * props.FontRenderingEmSize;
        }
        // Fallback: avares:// embedded fonts are not resolvable via FontManager.
        // DejaVu Sans Mono has a fixed advance of 1233 design units (em=2048)
        // for all glyphs, giving the correct monospace cell width.
        return 1233.0 / 2048.0 * props.FontRenderingEmSize;
    }
}
