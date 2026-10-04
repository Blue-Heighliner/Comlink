namespace BlueHeighliner.Comlink;

/// <summary>Helpers for the optional text an <see cref="IDisplayHandler"/> returns.</summary>
internal static class DisplayText
{
    extension(string? text)
    {
        /// <summary>Returns the text, or <see langword="null"/> when it is empty, so an empty answer keeps the engine's own text.</summary>
        public string? OrNull() => string.IsNullOrEmpty(text) ? null : text;
    }
}
