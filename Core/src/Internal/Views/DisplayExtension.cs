namespace BlueHeighliner.Comlink;

/// <summary>
/// XAML markup extension that shows a fixed piece of text, written with the engine's own names for concepts, as the host calls them (see <see cref="EngineControllerExtensions"/>), for
/// example <c>Content="{vm:Display 'NEW DRAFT'}"</c>. XAML has no access to the engine's services, so the engine hands it the rename once at startup through <see cref="Source"/>, the one global access point to it.
/// </summary>
/// <param name="text">The text, written with the engine's own names.</param>
[ExcludeFromCodeCoverage]
internal sealed class DisplayExtension(string text) : MarkupExtension
{
    /// <summary>Gets or sets how text is displayed. Set by the engine before any window is created; until then, and in tests, text is shown as written.</summary>
    public static Func<string, string> Source { get; set; } = text => text;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) => Source(text);
}
