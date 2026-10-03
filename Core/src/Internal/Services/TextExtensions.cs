namespace BlueHeighliner.Comlink;

/// <summary>Extensions over entry text.</summary>
internal static class TextExtensions
{
    extension(string? text)
    {
        /// <summary>Gets the trimmed first line of the text, or an empty string when it is null or blank. This is what an entry listing shows for a message, draft or note.</summary>
        public string FirstLine => (text ?? string.Empty).Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
    }
}
